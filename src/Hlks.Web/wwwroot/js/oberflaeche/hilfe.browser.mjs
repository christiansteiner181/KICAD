// Browser-Integrationstest der Hilfe (F1 und Symbol «?»): Edge headless über CDP, echte Maus- und Tastaturereignisse.
// Nicht Teil von «node --test». Der Test baut seine Umgebung selbst auf (Beispielprojekt, Server) und räumt nicht weg:
//   %TEMP%\HLKS_CAD\hilfe_test\ablage\Test_Hilfe 1\c_Pläne und Modelle\e_KICAD   Beispielprojekt (Mcp --beispiel)
//   %TEMP%\HLKS_CAD\hilfe_test\bib, \aus, \zuletzt, \katalog2                    Familienbibliothek, ausstehende Familien, Liste «zuletzt», Kataloge ohne Anleitung
// Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT = …\hilfe_test\wwwroot, Dateiwächter des Servers, siehe menueband.browser.mjs).
// Der Server läuft mit HLKS_TEST=1, HLKS_FAMILIENBIBLIOTHEK, HLKS_BIBLIOTHEK_AUSSTEHEND, HLKS_PROJEKTABLAGE und HLKS_ZULETZT unter %TEMP%: nie auf W:.
// Die Anleitung selbst (claude.ai) wird nie aufgerufen: window.open ist durch einen Stub ersetzt, der die Aufrufe sammelt.
// Aufruf:  node hilfe.browser.mjs        (Port 5196, zweiter Server 5197; HLKS_BUILD/HLKS_BAU wie beim Bauen, Vorgabe %USERPROFILE%\HLKS_CAD_build)
// Umgebung: SHOTS=<Ordner für Bilder> (Vorgabe %TEMP%\HLKS_CAD), HLKS_CDP_PORT (9540), HLKS_EDGE, HLKS_KATALOG (Vorgabe: kataloge/ dieses Quellbaums)
// Prüft: Adresse kommt mit /api/start/info; Klick auf «?» → Basisadresse (ohne Sprungziel, immer dasselbe Fenster «kicad-hilfe»); Maus über «Serien-PDF …»
// (Register Ansicht) + F1 → #k-ansicht~serien-pdf; Knopf in zusammengeklappter Gruppe (Flyout); Tastaturfokus auf einem Knopf (Schnellzugriff); Kontextregister
// «Ändern | <Klasse>»; offener Dialog + F1 → #s-<Titel> (Dialog geht vor dem Knopf unter dem Zeiger); F1 ohne Kontext; preventDefault; Pop-up gesperrt → Meldung
// mit Adresse; Adresse fehlt (Katalog ohne «anleitung», Startbildschirm ohne Projekt) → Meldung «noch nicht eingerichtet»; «?» auf dem Startbildschirm über dem
// offenen Projekt und im Zweitfenster; Tooltips «F1: Hilfe zu diesem Befehl» (Band, Flyout, Schnellzugriff; nicht bei «Zuletzt geöffnet»); Bilder hilfe_*.png.
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { hilfeSchluessel } from './hilfe.js';

const PORT = Number(process.env.HLKS_H_PORT ?? 5196);
const PORT2 = PORT + 1;
const SERVER = `http://localhost:${PORT}`;
const SERVER2 = `http://localhost:${PORT2}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9540);
if (![PORT, PORT2].every((p) => p >= 5196 && p <= 5199) || CDPPORT < 9540 || CDPPORT > 9549) { console.error('Ports nur 5196–5199 und CDP 9540–9549.'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'hilfe_test');
const hier = path.dirname(fileURLToPath(import.meta.url));
const KATALOG = process.env.HLKS_KATALOG ?? path.resolve(hier, '../../../../../kataloge');
mkdirSync(SHOTS, { recursive: true });

const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Umgebung aufbauen
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
for (const p of [PORT, PORT2]) spawnSync('powershell', ['-NoProfile', '-Command', `$c = Get-NetTCPConnection -State Listen -LocalPort ${p} -ErrorAction SilentlyContinue | Select-Object -First 1; if ($c) { Stop-Process -Id $c.OwningProcess -Force }`]);
rmSync(WURZEL, { recursive: true, force: true });
const ABLAGE = path.join(WURZEL, 'ablage'), BIB = path.join(WURZEL, 'bib'), AUS = path.join(WURZEL, 'aus'), ZULETZT = path.join(WURZEL, 'zuletzt'), ZULETZT2 = path.join(WURZEL, 'zuletzt2'), KAT2 = path.join(WURZEL, 'katalog2');
for (const d of [ABLAGE, BIB, AUS, ZULETZT, ZULETZT2]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_Hilfe 1', 'c_Pläne und Modelle', 'e_KICAD');
const MCP = path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll');
{
  const r = spawnSync(dotnet, [MCP, '--beispiel', projektOrdner], { encoding: 'utf8' });
  if (r.status !== 0) { console.error('Beispielprojekt nicht angelegt', r.stdout, r.stderr); process.exit(2); }
}
// Zweiter Katalogordner ohne «anleitung» (Adresse fehlt)
cpSync(KATALOG, KAT2, { recursive: true });
writeFileSync(path.join(KAT2, 'buero.json'), '{ "beschreibung": "Test ohne Anleitung" }');
const buero = JSON.parse(readFileSync(path.join(KATALOG, 'buero.json'), 'utf8'));
const BASIS = buero.anleitung?.url;
if (!BASIS) { console.error('kataloge/buero.json hat keine anleitung.url'); process.exit(2); }
cpSync(path.resolve(hier, '../..'), path.join(WURZEL, 'wwwroot'), { recursive: true });
const envBasis = { ...process.env, HLKS_WEBROOT: path.join(WURZEL, 'wwwroot'), HLKS_TEST: '1', HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: BIB, HLKS_BIBLIOTHEK_AUSSTEHEND: AUS };
const WEB = path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll');
const server = spawn(dotnet, [WEB, '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'H', '--geraet', 'h-test'], { env: { ...envBasis, HLKS_KATALOG: KATALOG, HLKS_ZULETZT: ZULETZT }, stdio: 'ignore' });
const server2 = spawn(dotnet, [WEB, '--port', String(PORT2), '--akteur', 'H', '--geraet', 'h-test2'], { env: { ...envBasis, HLKS_KATALOG: KAT2, HLKS_ZULETZT: ZULETZT2 }, stdio: 'ignore' });
for (const s of [SERVER, SERVER2]) for (let i = 0; i < 80; i++) { try { if ((await fetch(`${s}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
// Zwei Planblätter (FIKTIV), damit «Serien-PDF …» verfügbar ist und seinen Dialog öffnet
for (const [nummer, titel] of [['1-0001 H01', 'Heizung Erdgeschoss'], ['1-0001 S01', 'Sanitär Erdgeschoss']]) {
  const r = await (await fetch(`${SERVER}/api/befehl/planblatt_anlegen?trockenlauf=false`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ nummer, titel, format: 'A3' }) })).json();
  if (!r.angenommen) { console.error('Planblatt nicht angelegt', r.fehler, r.vorschlag); process.exit(2); }
}

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgeh-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1920,1000', '--no-first-run', 'about:blank'], { stdio: 'ignore' });
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); proc.kill(); server.kill(); server2.kill(); process.exit(2); }
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

const shot = async (name, clip = { x: 0, y: 0, width: aktuelleBreite, height: 86 }, scale = 1) => {
  const r = await send('Page.captureScreenshot', { format: 'png', clip: { ...clip, scale } });
  const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); return f;
};
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };

/** Ersetzt window.open durch einen Stub, der die Aufrufe sammelt (window.__hilfe); modus 'ok' gibt ein Fenster zurück, 'null' wie bei gesperrtem Pop-up. */
const stub = (modus = 'ok') => ausw(`window.__hilfe = []; window.__hilfeModus = ${JSON.stringify(modus)};
  window.open = (...a) => { window.__hilfe.push(a); return window.__hilfeModus === 'null' ? null : { focus() {}, closed: false }; }; return 1;`);
const aufrufe = () => ausw(`return window.__hilfe.map((a) => [...a]);`);
const leeren = () => ausw(`window.__hilfe.length = 0; return 1;`);

const gehe = async (url = `${SERVER}/`, { design = 'dunkel', bandErwartet = true } = {}) => {
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { if (!localStorage.getItem('hlks.design')) localStorage.setItem('hlks.design', JSON.stringify('${design}')); } catch (e) {}` }).catch(() => {});
  await send('Page.navigate', { url });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && !!document.querySelector('.hilfe-knopf') && (${bandErwartet ? 'z.zustand.elemente.size > 50 && !!document.querySelector(\'.mb-tab\')' : 'true'});`), 'App gestartet', 30000);
  await warte(700);
};
const mitte = (selektor, index = 0) => ausw(`const e = document.querySelectorAll(${JSON.stringify(selektor)})[${index}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
const bewegen = async (pos) => { await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y }); await warte(150); };
const ueber = async (selektor, index = 0) => {
  const pos = await mitte(selektor, index);
  if (!pos) throw new Error('Element nicht gefunden: ' + selektor);
  await bewegen(pos);
  const hover = await ausw(`return !!document.querySelector(${JSON.stringify(selektor)})?.matches(':hover');`);
  if (!hover) throw new Error('Zeiger steht nicht über ' + selektor);
};
const klickAn = async (pos) => {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y });
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: 'left', buttons: 1, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: 'left', buttons: 0, clickCount: 1 });
  await warte(250);
};
const klick = async (selektor, index = 0) => {
  const pos = await mitte(selektor, index);
  if (!pos) throw new Error('Element nicht gefunden: ' + selektor);
  const oben = await ausw(`const e = document.querySelectorAll(${JSON.stringify(selektor)})[${index}]; const t = document.elementFromPoint(${pos.x}, ${pos.y}); return !!t && (e === t || e.contains(t));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + selektor);
  await klickAn(pos);
};
const wegBewegen = () => bewegen({ x: 900, y: 700 });   // Zeiger in die Ansichtsfläche, über keinen Knopf
const taste = async (key, code, vk, modifiers = 0) => {
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers });
  await warte(250);
};
const f1 = () => taste('F1', 'F1', 112);
const esc = () => taste('Escape', 'Escape', 27);
const tab = () => taste('Tab', 'Tab', 9);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const gibt = async (sel) => (await anzahl(sel)) > 0;
const registerWahl = async (name) => { await ausw(`const t = [...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(name)}); if (!t) throw new Error('Register fehlt: ${name}'); t.click(); return 1;`); await warte(250); };
const info = () => ausw(`const m = await import('/js/oberflaeche/menueband.js'); return m.bandInfo();`);
const dialoge = () => anzahl('#dialogschicht .dlg');
const alleDialogeZu = async () => { for (let i = 0; i < 5 && (await dialoge()) > 0; i++) await esc(); };
const designSetzen = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}, false); return 1;`).then(() => warte(250));
const meldungen = () => ausw(`return [...document.querySelectorAll('#meldungen .meldung')].map((m) => m.innerText);`);
const meldungenLeeren = () => ausw(`document.querySelectorAll('#meldungen .meldung').forEach((m) => m.remove()); return 1;`);
const REGISTERNAME = { datei: 'Datei', architektur: 'Architektur', huelle: 'Hülle', gebaeudetechnik: 'Gebäudetechnik', einfuegen: 'Einfügen', beschriften: 'Beschriften', aendern: 'Ändern', koordination: 'Koordination', ansicht: 'Ansicht', verwalten: 'Verwalten' };

try {
  // ================================================================== Adresse vom Server
  abschnitt('Adresse der Anleitung kommt vom Server');
  const infoServer = await (await fetch(`${SERVER}/api/start/info`)).json();
  pruef(infoServer.anleitung?.url === BASIS && /^https:\/\/claude\.ai\/artifact\//.test(BASIS), '/api/start/info liefert anleitung.url aus kataloge/buero.json', JSON.stringify(infoServer.anleitung));
  pruef(/#k-<register>~<knopf>/.test(infoServer.anleitung?.hinweis ?? ''), '…und den Hinweis mit den Sprungzielen');
  const info2 = await (await fetch(`${SERVER2}/api/start/info`)).json();
  pruef(info2.anleitung == null, 'Katalog ohne «anleitung»: anleitung ist null', JSON.stringify(info2.anleitung));

  // ================================================================== Symbol «?»
  abschnitt('Symbol «?» in der Kopfleiste');
  await gehe();
  await stub();
  const kopf = await ausw(`const b = document.getElementById('hilfe-knopf'); const kr = document.getElementById('kopf-rechts'); const t = document.getElementById('titel');
    const r = b.getBoundingClientRect(); const k = document.getElementById('kopf').getBoundingClientRect(); const tr = t.getBoundingClientRect();
    const mitteX = r.left + r.width / 2, mitteY = r.top + r.height / 2; const oben = document.elementFromPoint(mitteX, mitteY);
    return { da: !!b, imKopfRechts: kr.contains(b), title: b.title, aria: b.getAttribute('aria-label'), svg: !!b.querySelector('svg'), rechtsVomTitel: r.left >= tr.right - 1, imKopf: r.top >= k.top && r.bottom <= k.bottom + 0.5,
      amRand: Math.round(k.right - r.right), sichtbar: r.width > 10 && r.height > 10 && oben === b || b.contains(oben) };`);
  console.log('   Knopf:', JSON.stringify(kopf));
  pruef(kopf.da && kopf.imKopfRechts, 'Das «?» sitzt in #kopf-rechts');
  pruef(kopf.rechtsVomTitel && kopf.imKopf && kopf.amRand < 20, 'Rechts vom Titel, innerhalb der Kopfleiste, am rechten Rand');
  pruef(kopf.title === 'Hilfe – Bedienungsanleitung (F1)' && kopf.aria === kopf.title, 'Tooltip «Hilfe – Bedienungsanleitung (F1)»', kopf.title);
  pruef(kopf.svg && kopf.sichtbar, 'Eigenes Symbol (SVG), sichtbar und nicht verdeckt');
  const symbolPfad = await ausw(`return (await import('/js/oberflaeche/symbole.js')).symbol('anleitung', 18) !== (await import('/js/oberflaeche/symbole.js')).symbol('unbekannt-xyz', 18);`);
  pruef(symbolPfad, 'symbole.js kennt das Symbol «anleitung» (nicht der Platzhalter)');

  await designSetzen('hell');
  await shot('hilfe_kopf_hell');
  await shot('hilfe_kopf_hell_zoom', { x: aktuelleBreite - 320, y: 0, width: 320, height: 34 }, 4);
  await designSetzen('dunkel');
  await shot('hilfe_kopf_dunkel');
  await shot('hilfe_kopf_dunkel_zoom', { x: aktuelleBreite - 320, y: 0, width: 320, height: 34 }, 4);

  await klick('#hilfe-knopf');
  let a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS && a[0][1] === 'kicad-hilfe' && a[0].length === 2, 'Klick auf «?» öffnet die Basisadresse ohne Sprungziel im Fenster «kicad-hilfe»', JSON.stringify(a));
  await klick('#hilfe-knopf');
  a = await aufrufe();
  pruef(a.length === 2 && a[1][1] === 'kicad-hilfe', 'Ein zweiter Klick benutzt wieder dasselbe Fenster (gleicher Name)', JSON.stringify(a));

  // ================================================================== F1 über einem Knopf
  abschnitt('F1 über einem Knopf des Menübands');
  await leeren();
  await registerWahl('Ansicht');
  await ueber('.mb-feld .mb-knopf[data-id="plan.serienpdf"]');
  const tip = await ausw(`return document.querySelector('.mb-feld .mb-knopf[data-id="plan.serienpdf"]').title;`);
  pruef(tip.endsWith('\nF1: Hilfe zu diesem Befehl'), 'Tooltip von «Serien-PDF …» endet mit der Zeile «F1: Hilfe zu diesem Befehl»', JSON.stringify(tip));
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === `${BASIS}#k-ansicht~serien-pdf` && a[0][1] === 'kicad-hilfe', 'Maus über «Serien-PDF …» (Ansicht) + F1 → …#k-ansicht~serien-pdf', JSON.stringify(a));

  await leeren();
  await registerWahl('Gebäudetechnik');
  const gtKnopf = await ausw(`const k = document.querySelector('.mb-feld .mb-knopf'); return k ? { id: k.dataset.id, titel: (await import('/js/oberflaeche/aktionen.js')).aktionen.get(k.dataset.id).titel.replace(/[\\u200b\\u00ad]/g, '') } : null;`);
  await ueber('.mb-feld .mb-knopf');
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === `${BASIS}#k-gebaeudetechnik~${hilfeSchluessel(gtKnopf.titel)}`, `Register «Gebäudetechnik», Knopf «${gtKnopf.titel}» → #k-gebaeudetechnik~${hilfeSchluessel(gtKnopf.titel)}`, JSON.stringify(a));

  // ---------------------------------------------------------------- Zeiger weg → kein Kontext
  abschnitt('F1 ohne Kontext und preventDefault');
  await leeren();
  await wegBewegen();
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS && a[0][1] === 'kicad-hilfe', 'F1 mit dem Zeiger in der Ansichtsfläche (kein Knopf, kein Dialog) → Basisadresse', JSON.stringify(a));
  const verhindert = await ausw(`const ev = new KeyboardEvent('keydown', { key: 'F1', code: 'F1', cancelable: true, bubbles: true }); document.body.dispatchEvent(ev); return ev.defaultPrevented;`);
  pruef(verhindert === true, 'F1 ruft preventDefault auf (Edge öffnet nicht seine eigene Hilfe)');
  await leeren();
  const ohneVerhindert = await ausw(`const ev = new KeyboardEvent('keydown', { key: 'F2', code: 'F2', cancelable: true, bubbles: true }); document.body.dispatchEvent(ev); return ev.defaultPrevented;`);
  pruef(ohneVerhindert === false && (await aufrufe()).length === 0, 'Andere Funktionstasten (F2) bleiben unberührt');
  const mitStrg = await ausw(`const ev = new KeyboardEvent('keydown', { key: 'F1', code: 'F1', ctrlKey: true, cancelable: true, bubbles: true }); document.body.dispatchEvent(ev); return ev.defaultPrevented;`);
  pruef(mitStrg === false && (await aufrufe()).length === 0, 'Strg+F1 bleibt unberührt');

  // ---------------------------------------------------------------- Tastaturfokus
  abschnitt('Tastaturfokus auf einem Knopf');
  await leeren();
  await wegBewegen();
  await ausw(`document.activeElement?.blur?.(); return 1;`);
  let fokusKnopf = null;
  for (let i = 0; i < 12; i++) {
    await tab();
    fokusKnopf = await ausw(`const a = document.activeElement; return a?.classList.contains('mb-knopf') ? { id: a.dataset.id, imBand: !!a.closest('#menueband'), feldPos: a.matches(':focus-visible') } : null;`);
    if (fokusKnopf) break;
  }
  console.log('   Fokus:', JSON.stringify(fokusKnopf));
  pruef(!!fokusKnopf && fokusKnopf.feldPos, 'Mit Tab ist ein Knopf des Menübands/Schnellzugriffs fokussiert (focus-visible)');
  if (fokusKnopf) {
    const erw = await ausw(`const a = (await import('/js/oberflaeche/aktionen.js')).aktionen.get(${JSON.stringify(fokusKnopf.id)}); return { titel: a.titel.replace(/[\\u200b\\u00ad]/g, ''), reg: a.register };`);
    const regName = fokusKnopf.imBand ? await ausw(`return document.querySelector('.mb-tab.aktiv').textContent;`) : REGISTERNAME[erw.reg];
    await leeren();
    await f1();
    a = await aufrufe();
    const sollte = `${BASIS}#k-${hilfeSchluessel(regName)}~${hilfeSchluessel(erw.titel)}`;
    pruef(a.length === 1 && a[0][0] === sollte, `Tastaturfokus auf «${erw.titel}» (${regName}) + F1 → ${sollte.slice(BASIS.length)}`, JSON.stringify(a));
  }
  // Fokus nach einem Mausklick (nicht focus-visible) zählt nicht, wenn der Zeiger anderswo steht
  await ausw(`document.activeElement?.blur?.(); return 1;`);
  await registerWahl('Ansicht');
  await klick('.mb-feld .mb-knopf[data-id="ansicht.nebeneinander"]');
  await wegBewegen();
  await leeren();
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS, 'Nach einem Mausklick auf einen Knopf (Zeiger inzwischen woanders) gilt der Rest-Fokus nicht', JSON.stringify(a));
  await ausw(`document.activeElement?.blur?.(); return 1;`);

  // ---------------------------------------------------------------- Kontextregister
  abschnitt('Kontextregister «Ändern | <Klasse>»');
  await registerWahl('Ändern');
  const sel = await ausw(`const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.klasse === 'Raum') ?? [...z.zustand.elemente.values()].find((x) => x.klasse === 'Rohr'); z.auswaehlen(e.id); return { klasse: e.klasse };`);
  await warteBis(() => gibt('.mb-tab.kontext.aktiv'), 'Kontextregister aktiv');
  await warte(300);
  const kTab = await ausw(`return document.querySelector('.mb-tab.kontext.aktiv').textContent;`);
  pruef(kTab === `Ändern | ${sel.klasse}`, `Kontextregister heisst «${kTab}»`);
  const kKnopf = await ausw(`const k = document.querySelector('.mb-feld .mb-knopf'); return k ? { id: k.dataset.id, titel: (await import('/js/oberflaeche/aktionen.js')).aktionen.get(k.dataset.id).titel.replace(/[\\u200b\\u00ad]/g, '') } : null;`);
  await leeren();
  await ueber('.mb-feld .mb-knopf');
  await f1();
  a = await aufrufe();
  const kSoll = `${BASIS}#k-${hilfeSchluessel(kTab)}~${hilfeSchluessel(kKnopf.titel)}`;
  pruef(a.length === 1 && a[0][0] === kSoll, `Knopf «${kKnopf.titel}» im Kontextregister + F1 → ${kSoll.slice(BASIS.length)}`, JSON.stringify(a));
  await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return 1;`);
  await warte(300);

  // ================================================================== Flyout
  abschnitt('Knopf in einer zusammengeklappten Gruppe (Flyout)');
  let gefunden = null;
  for (const w of [900, 800, 700, 600, 500, 440, 400, 360]) {
    await breite(w);
    await registerWahl('Ansicht');
    const g = (await info()).gruppen.find((x) => x.name === 'Planblätter');
    if (g && (g.stufe === 'Z' || g.stufe === 'W')) { gefunden = { w, stufe: g.stufe }; break; }
  }
  pruef(!!gefunden, 'Es gibt eine Breite, bei der «Planblätter» zusammengeklappt (Z/W) ist', JSON.stringify(gefunden));
  if (gefunden) {
    console.log('   Breite', gefunden.w, 'Stufe', gefunden.stufe);
    await klick(gefunden.stufe === 'Z' ? '.mb-feld .mb-zu[data-gruppe="Planblätter"]' : '.mb-feld .mb-weitere');
    await warteBis(() => gibt('.mb-flyout'), 'Flyout offen');
    await leeren();
    await ueber('.mb-flyout .mb-knopf[data-id="plan.serienpdf"]');
    const ftip = await ausw(`return document.querySelector('.mb-flyout .mb-knopf[data-id="plan.serienpdf"]').title;`);
    pruef(ftip.endsWith('\nF1: Hilfe zu diesem Befehl'), 'Auch im Flyout endet der Tooltip mit «F1: Hilfe zu diesem Befehl»', JSON.stringify(ftip));
    await f1();
    a = await aufrufe();
    pruef(a.length === 1 && a[0][0] === `${BASIS}#k-ansicht~serien-pdf`, 'Maus über «Serien-PDF …» im Flyout + F1 → …#k-ansicht~serien-pdf', JSON.stringify(a));
    pruef(await gibt('.mb-flyout'), 'F1 schliesst das Flyout nicht');
    await esc();
  }
  await breite(1920);
  await registerWahl('Ansicht');

  // ================================================================== Dialog
  abschnitt('Offener Dialog');
  await leeren();
  await klick('.mb-feld .mb-knopf[data-id="plan.serienpdf"]');   // Zeiger bleibt auf dem Knopf, der den Dialog geöffnet hat
  let dlgTitel = null;
  try { await warteBis(async () => (await dialoge()) > 0, 'Dialog Serien-PDF offen', 8000); dlgTitel = await ausw(`return document.querySelector('#dialogschicht .dlg:last-child .dlg-titel').textContent;`); } catch { /* unten gemeldet */ }
  console.log('   Dialog:', JSON.stringify(dlgTitel));
  pruef(!!dlgTitel, '«Serien-PDF …» öffnet einen Dialog');
  if (dlgTitel) {
    await shot('hilfe_dialog_offen', { x: 0, y: 0, width: aktuelleBreite, height: 600 });
    await f1();
    a = await aufrufe();
    pruef(a.length === 1 && a[0][0] === `${BASIS}#s-${hilfeSchluessel(dlgTitel)}`, `Dialog «${dlgTitel}» offen (Zeiger noch auf dem Knopf) + F1 → #s-${hilfeSchluessel(dlgTitel)}`, JSON.stringify(a));
    await wegBewegen();
    await leeren();
    await f1();
    a = await aufrufe();
    pruef(a.length === 1 && a[0][0] === `${BASIS}#s-${hilfeSchluessel(dlgTitel)}`, 'Dasselbe mit dem Zeiger ausserhalb des Menübands', JSON.stringify(a));
    await alleDialogeZu();
    pruef((await dialoge()) === 0, 'Dialog geschlossen');
    // Esc gibt den Fokus an den Knopf zurück, der den Dialog geöffnet hat: das ist Tastaturfokus auf einem Knopf und zählt wie dort
    await leeren();
    await f1();
    a = await aufrufe();
    pruef(a.length === 1 && a[0][0] === `${BASIS}#k-ansicht~serien-pdf`, 'Nach Esc steht der Fokus wieder auf dem Knopf (Zeiger woanders): F1 gilt diesem Knopf', JSON.stringify(a));
    await ausw('document.activeElement?.blur?.(); return 1;');
    await leeren();
    await f1();
    a = await aufrufe();
    pruef(a.length === 1 && a[0][0] === BASIS, 'Ohne Fokus auf einem Knopf gilt nach dem Schliessen wieder die Basisadresse', JSON.stringify(a));
  }

  abschnitt('Zweiter Dialog (Tastenkürzel) und Dialog über Dialog');
  await registerWahl('Verwalten');
  await klick('.mb-feld .mb-knopf[data-id="verwalten.kuerzel"]');
  await warteBis(async () => (await dialoge()) > 0, 'Dialog Kürzel offen', 8000);
  const dlg2 = await ausw(`return document.querySelector('#dialogschicht .dlg:last-child .dlg-titel').textContent;`);
  await leeren();
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === `${BASIS}#s-${hilfeSchluessel(dlg2)}`, `Dialog «${dlg2}» + F1 → #s-${hilfeSchluessel(dlg2)}`, JSON.stringify(a));
  // Ein Dialog über dem Dialog: der oberste zählt
  const oben = await ausw(`const d = await import('/js/oberflaeche/dialoge.js'); d.zeigen({ titel: 'Prüfdialog «Äußerer Test»', inhalt: '<p>x</p>' }); await new Promise((r) => setTimeout(r, 200)); return d.obersterDialogTitel();`);
  pruef(oben === 'Prüfdialog «Äußerer Test»', 'obersterDialogTitel() liefert den obersten Dialog', oben);
  await leeren();
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === `${BASIS}#s-pruefdialog-aeusserer-test`, 'Zwei Dialoge offen: der oberste zählt (#s-pruefdialog-aeusserer-test)', JSON.stringify(a));
  await alleDialogeZu();

  // ================================================================== Pop-up gesperrt
  abschnitt('Pop-up gesperrt');
  await stub('null');
  await meldungenLeeren();
  await wegBewegen();
  await leeren();
  await f1();
  a = await aufrufe();
  let m = await meldungen();
  pruef(a.length === 1 && m.length === 1 && m[0].includes('Das Hilfefenster wurde blockiert') && m[0].includes(`Adresse: ${BASIS}`), 'window.open liefert null → Meldung «Das Hilfefenster wurde blockiert – Adresse: …»', JSON.stringify(m));
  await meldungenLeeren();
  await registerWahl('Ansicht');
  await ueber('.mb-feld .mb-knopf[data-id="plan.serienpdf"]');
  await f1();
  m = await meldungen();
  pruef(m.length === 1 && m[0].includes(`Adresse: ${BASIS}#k-ansicht~serien-pdf`), 'Die Meldung nennt die volle Adresse mit Sprungziel', JSON.stringify(m));
  await shot('hilfe_meldung_blockiert', { x: aktuelleBreite - 520, y: 600, width: 520, height: 400 });
  await meldungenLeeren();
  await stub('ok');

  // ================================================================== Startbildschirm über dem offenen Projekt
  abschnitt('Startbildschirm');
  await gehe(`${SERVER}/?start=1`);
  await stub();
  await warteBis(() => ausw(`const s = document.getElementById('startbildschirm'); return !!s && !s.hidden;`), 'Startbildschirm sichtbar', 15000);
  await warte(400);
  const sk = await ausw(`const b = document.getElementById('hilfe-knopf'); const r = b.getBoundingClientRect(); const t = document.elementFromPoint(r.left + r.width / 2, r.top + r.height / 2); return { sichtbar: r.width > 10, frei: t === b || b.contains(t) };`);
  pruef(sk.sichtbar && sk.frei, 'Auf dem Startbildschirm ist das «?» sichtbar und nicht verdeckt', JSON.stringify(sk));
  await klick('#hilfe-knopf');
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS, 'Klick auf «?» auf dem Startbildschirm öffnet die Anleitung', JSON.stringify(a));
  await leeren();
  await wegBewegen();
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS, 'F1 auf dem Startbildschirm öffnet die Anleitung', JSON.stringify(a));
  await shot('hilfe_start_dunkel', { x: 0, y: 0, width: aktuelleBreite, height: 200 });

  // ================================================================== Adresse fehlt: Server ohne Projekt, Katalog ohne Anleitung
  abschnitt('Adresse fehlt (Startbildschirm ohne Projekt)');
  await gehe(`${SERVER2}/`, { bandErwartet: false });
  await stub();
  await warteBis(() => ausw(`const s = document.getElementById('startbildschirm'); return !!s && !s.hidden;`), 'Startbildschirm ohne Projekt', 15000);
  await meldungenLeeren();
  await klick('#hilfe-knopf');
  a = await aufrufe();
  m = await meldungen();
  pruef(a.length === 0 && m.length === 1 && m[0].includes('Die Bedienungsanleitung ist noch nicht eingerichtet'), 'Ohne Adresse: «?» öffnet kein Fenster, Meldung «Die Bedienungsanleitung ist noch nicht eingerichtet»', JSON.stringify([a, m]));
  await meldungenLeeren();
  await wegBewegen();
  await f1();
  a = await aufrufe();
  m = await meldungen();
  pruef(a.length === 0 && m.length === 1 && m[0].includes('noch nicht eingerichtet'), 'Ohne Adresse: auch F1 meldet es und öffnet nichts', JSON.stringify([a, m]));
  await shot('hilfe_meldung_nicht_eingerichtet', { x: aktuelleBreite - 520, y: 600, width: 520, height: 400 });

  // ================================================================== Zweitfenster
  abschnitt('Zweitfenster (?ansicht=…&fenster=2)');
  await gehe();   // erst das Hauptfenster, um eine Ansicht zu kennen
  const ansicht = await ausw(`const z = await import('/js/zustand.js'); return [...z.zustand.elemente.values()].find((e) => e.klasse === 'Ansicht' && e.werte?.art === 'grundriss' && !e.werte?.vorlage)?.kennzeichen ?? null;`);
  pruef(!!ansicht, 'Eine Grundrissansicht für das Zweitfenster gefunden', String(ansicht));
  await gehe(`${SERVER}/?ansicht=${encodeURIComponent(ansicht)}&fenster=2`, { bandErwartet: false });
  await stub();
  await warte(600);
  const zw = await ausw(`const b = document.getElementById('hilfe-knopf'); const r = b.getBoundingClientRect(); const z = document.querySelector('.ag-zurueck'); return { fenster: document.body.dataset.fenster, da: !!b, sichtbar: r.width > 10, nachZurueck: !z || z.getBoundingClientRect().right <= r.left + 1 };`);
  pruef(zw.fenster === '2' && zw.da && zw.sichtbar, 'Im Zweitfenster gibt es das «?» (Modus «nur Ansicht»)', JSON.stringify(zw));
  pruef(zw.nachZurueck, '…rechts neben «Zurück ins Hauptfenster»');
  await klick('#hilfe-knopf');
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS && a[0][1] === 'kicad-hilfe', 'Zweitfenster: Klick auf «?» öffnet die Basisadresse', JSON.stringify(a));
  await leeren();
  await wegBewegen();
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === BASIS && a[0][1] === 'kicad-hilfe', 'Zweitfenster: F1 öffnet die Basisadresse', JSON.stringify(a));
  await leeren();
  await ausw(`const d = await import('/js/oberflaeche/dialoge.js'); d.zeigen({ titel: 'Test im Zweitfenster', inhalt: '<p>x</p>' }); await new Promise((r) => setTimeout(r, 200)); return 1;`);
  await f1();
  a = await aufrufe();
  pruef(a.length === 1 && a[0][0] === `${BASIS}#s-test-im-zweitfenster`, 'Zweitfenster: Dialog offen + F1 → #s-…', JSON.stringify(a));
  await alleDialogeZu();

  // ================================================================== Tooltips im Band
  abschnitt('Tooltips der Menüband-Knöpfe');
  await gehe();
  await breite(1920);
  const registerListe = await ausw(`return [...document.querySelectorAll('.mb-tab')].map((t) => t.textContent);`);
  let gesamt = 0; const ohne = [];
  for (const r of registerListe) {
    await registerWahl(r);
    const l = await ausw(`return [...document.querySelectorAll('.mb-feld .mb-knopf')].map((k) => ({ id: k.dataset.id, ordner: !!k.dataset.ordner, titel: k.title }));`);
    for (const k of l) { gesamt++; const hat = k.titel.endsWith('\nF1: Hilfe zu diesem Befehl'); if (k.ordner ? k.titel.includes('F1:') : !hat) ohne.push(`${r}/${k.id}`); }
  }
  console.log(`   ${gesamt} Knöpfe in ${registerListe.length} Registern geprüft`);
  pruef(gesamt > 40 && ohne.length === 0, 'Alle Knöpfe (alle Register) haben als letzte Tooltip-Zeile «F1: Hilfe zu diesem Befehl»; Einträge von «Zuletzt geöffnet» nicht', ohne.slice(0, 5).join(', '));
  const sz = await ausw(`return [...document.querySelectorAll('#schnellzugriff .mb-knopf')].map((k) => k.title);`);
  pruef(sz.length >= 3 && sz.every((t) => t.endsWith('\nF1: Hilfe zu diesem Befehl')), 'Auch die Knöpfe des Schnellzugriffs', JSON.stringify(sz));
  const gesperrt = await ausw(`const k = document.querySelector('.mb-feld .mb-knopf.gesperrt'); return k ? k.title : null;`);
  pruef(gesperrt === null || (gesperrt.includes('\n') && gesperrt.endsWith('\nF1: Hilfe zu diesem Befehl')), 'Bei gesperrten Knöpfen steht die F1-Zeile nach dem Sperrhinweis', JSON.stringify(gesperrt));
  const zuletzt = await ausw(`const k = document.querySelector('.mb-knopf[data-ordner]'); return k ? k.title : null;`);
  void zuletzt;

  // ================================================================== Band intakt
  abschnitt('Band nach den Eingriffen intakt');
  await registerWahl('Ansicht');
  const anzTabs = await anzahl('.mb-tab');
  pruef(anzTabs === 10 || anzTabs === 11, 'Kein zusätzliches Register im Menüband', String(anzTabs));
  const gruppenAnsicht = (await info()).gruppen.map((g) => g.name).join(',');
  pruef(!/hilfe|anleitung/i.test(gruppenAnsicht), 'Keine zusätzliche Gruppe im Register Ansicht', gruppenAnsicht);

  abschnitt('Konsole');
  pruef(fehler.length === 0, 'Keine Ausnahmen in der Seite', fehler.slice(0, 3).join(' | '));
  const echteFehler = konsole.filter((k) => !/Failed to load resource|favicon/.test(k));
  pruef(echteFehler.length === 0, 'Keine Fehler in der Konsole', echteFehler.slice(0, 3).join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Ausnahme im Test:', e.message);
  try { await shot('hilfe_99_fehler', { x: 0, y: 0, width: aktuelleBreite, height: 700 }); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
proc.kill(); server.kill(); server2.kill();
await warte(500);
try { rmSync(profil, { recursive: true, force: true }); } catch { /* egal */ }
process.exit(schlecht > 0 ? 1 : 0);
