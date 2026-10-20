// Browser-Integrationstest der Oberfläche der Sanitär-Belastungswerte (Modul 4 Teil A-O): Edge headless über CDP, echte Mausklicks und Tastatur, Beispielprojekt «MFH Musterweg»
// (16 Sanitärapparate in Anlage A-SAN, K 0.5 «fiktiv wie Walkringen»). Nicht Teil von «node --test». Aufruf:  node sanitaer-belastung.browser.mjs
// (Port 5211, CDP 9560; HLKS_M4_PORT, HLKS_CDP_PORT; HLKS_BUILD wie beim Bauen; HLKS_KATALOG auf die Kataloge der Arbeitskopie, sonst nimmt der Kern den Katalog des Haupt-Repos).
// Der Server läuft mit HLKS_TEST=1 und allen Ablageorten unter %TEMP%\HLKS_CAD\m4_test – nie auf W:. Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT).
//
// Prüft (Teil A-O der Spezifikation entwurf/modul-4.md):
//  1. Eigenschaften eines Apparats: Apparateart als Auswahl mit Namen, «Vorgabe der Klasse (…)», LU KW / LU WW / DU abgeleitet mit «?» (Quelle «LU_DU Zusammenstellung_.xlsx», Zeile n);
//     Art ändern → Werte und Summen der Leitungen ändern sich sofort, zurück zur Vorgabe.
//  2. Eigenschaften einer Leitung: ΣLU KW / WW, grösster Einzel-LU bzw. ΣDU mit «?» (Rechenweg, Beiträge der Apparate).
//  3. Eigenschaften der Anlage: Abflusskennzahl K als freie Zahl mit Hinweis, Tabelle der Spezialinstallationen (Zeile hinzufügen/löschen, kein «[object Object]»).
//  4. Strangschema: Beschriftung «ΣLU 24», «ΣDU 16.7», Schalter belastung (Palette der Ansicht) ein/aus, Art ändern wirkt auf die Beschriftung.
//  5. Liste «Sanitär-Zusammenstellung (W3)»: Dialog «Liste anlegen», Aufbau wie das Excel mit den Zahlen des Beispielprojekts, Tooltips mit Formel und Excel-Zelle, CSV mit beiden Blöcken,
//     Spezialinstallation ändert die Spitze gesamt, K leer → «K fehlt» (kein Q_WW, kein Q_tot) und Befund P25 im Dialog Prüfbefunde.
//  6. Knopf «Sanitär-Zusammenstellung» (Gebäudetechnik › Apparate und Armaturen) öffnet die vorhandene Liste; Listen sind (wie alle) nicht auf Planblätter platzierbar.
// Bilder (hell und dunkel): %TEMP%\HLKS_CAD\m4_*.png (SHOTS ändert den Ordner). Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, readdirSync, existsSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_M4_PORT ?? 5211);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9560);
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152].includes(PORT)) { console.error('Port ' + PORT + ' ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }   // nur die festen Sperren; Bereich je Auftrag über HLKS_M4_PORT/HLKS_CDP_PORT
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'm4_test');
const hier = path.dirname(fileURLToPath(import.meta.url));
mkdirSync(SHOTS, { recursive: true });

const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Umgebung aufbauen
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
spawnSync('powershell', ['-NoProfile', '-Command', `$c = Get-NetTCPConnection -State Listen -LocalPort ${PORT} -ErrorAction SilentlyContinue | Select-Object -First 1; if ($c) { Stop-Process -Id $c.OwningProcess -Force }`]);
rmSync(WURZEL, { recursive: true, force: true });
const ABLAGE = path.join(WURZEL, 'ablage'), DL = path.join(WURZEL, 'downloads');
for (const d of [ABLAGE, DL, path.join(WURZEL, 'bib'), path.join(WURZEL, 'aus'), path.join(WURZEL, 'zuletzt'), path.join(WURZEL, 'normen')]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_M4_Weg 1', 'c_Pläne und Modelle', 'e_KICAD');
const ENV = {
  ...process.env, HLKS_TEST: '1', HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: path.join(WURZEL, 'bib'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(WURZEL, 'aus'),
  HLKS_ZULETZT: path.join(WURZEL, 'zuletzt'), HLKS_BENUTZER: path.join(WURZEL, 'benutzer'), HLKS_ANLEITUNG: path.join(WURZEL, 'anleitung'),
  HLKS_ANLEITUNG_PRUEFUNG: path.join(WURZEL, 'anleitung_pruefung'), HLKS_NORMEN: path.join(WURZEL, 'normen'),
};
{
  const r = spawnSync(dotnet, [path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', projektOrdner], { encoding: 'utf8', env: ENV });
  if (r.status !== 0) { console.error('Beispielprojekt nicht angelegt', r.stdout, r.stderr); process.exit(2); }
}
cpSync(path.resolve(hier, '../..'), path.join(WURZEL, 'wwwroot'), { recursive: true });
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'M4', '--geraet', 'm4-test'],
  { env: { ...ENV, HLKS_WEBROOT: path.join(WURZEL, 'wwwroot') }, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const kern = async (art, name, body) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const befehl = async (name, body) => { const r = await kern('befehl', name, body); if (!r.angenommen) throw new Error(`${name}: ${r.fehler ?? r.meldung}`); return r; };

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgem4-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1920,1000', '--no-first-run', '--enable-unsafe-swiftshader', 'about:blank'], { stdio: 'ignore' });
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
async function aufraeumen(code) {
  try { proc.kill(); } catch { /* egal */ }
  try { server.kill(); } catch { /* egal */ }
  await warte(800);
  try { rmSync(profil, { recursive: true, force: true }); } catch { /* egal */ }
  process.exit(code);
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
await send('Page.setDownloadBehavior', { behavior: 'allow', downloadPath: DL }).catch(async () => { await send('Browser.setDownloadBehavior', { behavior: 'allow', downloadPath: DL }); });
const breite = async (w, h = 1000) => { await send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false }); await warte(350); };
await breite(1920, 1300);

const speichern = async (name, clip, scale = 1) => {
  const r = await send('Page.captureScreenshot', { format: 'png', ...(clip ? { clip: { ...clip, scale } } : {}) });
  const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); return f;
};
const bilder = [];
const rechteck = (sel, rand = 8) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; const r = e.getBoundingClientRect();
  const x = Math.max(0, Math.floor(r.left - ${rand})), y = Math.max(0, Math.floor(r.top - ${rand})); return { x, y, width: Math.min(innerWidth - x, Math.ceil(r.width + 2 * ${rand})), height: Math.min(innerHeight - y, Math.ceil(r.height + 2 * ${rand})) };`);
const designSetzen = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}, false); return 1;`).then(() => warte(300));
/** Bild eines Ausschnitts (Selektor; ohne: ganze Seite) in beiden Designs: m4_<name>_dunkel.png und m4_<name>_hell.png. */
async function zweiBilder(name, sel = null, rand = 8) {
  for (const d of ['dunkel', 'hell']) {
    await designSetzen(d);
    const clip = sel ? await rechteck(sel, rand) : null;
    bilder.push(await speichern(`m4_${name}_${d}`, clip));
  }
  await designSetzen('dunkel');
}
/** Ausschnitt um ein Element der Seite, vergrössert (scale), in beiden Designs: m4_<name>_dunkel.png / _hell.png. */
async function detailBilder(name, sel, rand, scale) {
  for (const d of ['dunkel', 'hell']) {
    await designSetzen(d);
    const clip = await rechteck(sel, rand);
    bilder.push(await speichern(`m4_${name}_${d}`, clip, scale));
  }
  await designSetzen('dunkel');
}
const bildPruefen = (pfad) => { const b = readFileSync(pfad); return b.subarray(0, 8).toString('hex') === '89504e470d0a1a0a' && b.length > 1500; };

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
const tab = () => taste('Tab', 'Tab', 9);
const mitte = (selektor, index = 0) => ausw(`const e = document.querySelectorAll(${JSON.stringify(selektor)})[${index}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
const klickAn = async (pos, opt = {}) => {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y });
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: 'left', buttons: 1, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: 'left', buttons: 0, clickCount: 1 });
  await warte(opt.warten ?? 250);
};
const klick = async (selektor, index = 0, opt = {}) => {
  await ausw(`document.querySelectorAll(${JSON.stringify(selektor)})[${index}]?.scrollIntoView({ block: 'center' }); return 1;`);   // in Paletten und Listen liegt vieles ausserhalb des sichtbaren Teils
  await warte(60);
  const pos = await mitte(selektor, index);
  if (!pos) throw new Error('Element nicht gefunden: ' + selektor);
  await klickAn(pos, opt);
};
/** Tippt in ein Feld (echte Mausklicks und Zeichen) und verlässt es mit Tab: das übernimmt der change-Hörer der Palette. */
async function tippe(sel, text, verlassen = true) {
  await klick(sel);
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.focus(); if (e.select) e.select(); return true;`);
  if (text === '') { await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 }); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 }); }
  else await send('Input.insertText', { text });
  await warte(150);
  if (verlassen) await tab();
  await warte(250);
}
const wahl = (sel, wert) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('fehlt: ${sel}'); e.value = ${JSON.stringify(wert)}; e.dispatchEvent(new Event('change', { bubbles: true })); return e.value;`).then((v) => warte(300).then(() => v));
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const gibt = async (sel) => (await anzahl(sel)) > 0;
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const wert = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.value ?? null;`);
const registerWahl = async (name) => { await ausw(`const t = [...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(name)}); if (!t) throw new Error('Register fehlt: ${name}'); t.click(); return 1;`); await warte(250); };
const dialoge = () => anzahl('#dialogschicht .dlg');
const dlgTitel = () => ausw(`const d = [...document.querySelectorAll('#dialogschicht .dlg')].at(-1); return d?.querySelector('.dlg-titel')?.textContent ?? null;`);
const alleDialogeZu = async () => { for (let i = 0; i < 5 && (await dialoge()) > 0; i++) await esc(); };
async function dlgKnopf(beschriftung) {
  const pos = await ausw(`const d = [...document.querySelectorAll('#dialogschicht .dlg')].at(-1); const k = [...(d?.querySelectorAll('.dlg-knopf') ?? [])].find((b) => b.textContent.trim() === ${JSON.stringify(beschriftung)}); if (!k) return null; const r = k.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
  if (!pos) throw new Error('Dialogknopf fehlt: ' + beschriftung);
  await klickAn(pos, { warten: 400 });
}
const knopfSel = (id) => `.mb-feld .mb-knopf[data-id="${id}"]`;
const dateiWarten = async (name, ms = 10000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { const f = path.join(DL, name); if (existsSync(f) && !readdirSync(DL).some((x) => x.endsWith('.crdownload'))) return f; await warte(150); } return null; };

// ------------------------------------------------------------------ Hilfen für Palette, Schema und Liste
const PAL = '#palette-eigenschaften';
const waehle = async (kz) => {
  await ausw(`const z = await import('/js/zustand.js'); const e = z.element(${JSON.stringify(kz)}); if (!e) throw new Error('Element fehlt: ${kz}'); z.auswaehlen(e.id); return 1;`);
  await warteBis(async () => (await wert(`${PAL} [data-ziel="kz"]`)) === kz, `Palette zeigt ${kz}`);
  await warte(250);
};
const waehleAnsicht = async (kz) => {   // Palette der Ansicht: nichts gewählt, Ansicht aktiv
  await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return 1;`);
  await warteBis(async () => (await wert(`${PAL} [data-ziel="kz"]`)) === kz, `Palette zeigt ${kz}`);
};
/** Zeile der Palette nach Klassenname (ep-san-luKw …): Text des Werts. */
const wertText = (name) => text(`${PAL} .ep-san-${name} .ep-text`);
const paletteText = () => text(`${PAL} .ep-inhalt`);
const lesen = (kz) => kern('abfrage', 'element_lesen', { element: kz });
const zusammenstellung = (anlage = 'A-SAN') => kern('abfrage', 'sanitaer_zusammenstellung', { anlage });
const plangrafik = (kz) => fetch(`${SERVER}/api/plangrafik/${kz}`).then((r) => r.json());
const sigmaTexte = async (kz) => (await plangrafik(kz)).teile.flatMap((t) => t.primitive.filter((p) => p.art === 'text' && /^Σ/.test(p.text ?? '')).map((p) => p.text));
const svgSigma = () => ausw(`return [...document.querySelectorAll('.kachel.aktiv svg text')].map((t) => t.textContent).filter((t) => /^Σ/.test(t));`);
const sw = (s) => (s === undefined || s === null ? null : s);
const warumOeffnen = async (zeilenKlasse) => {
  await klick(`${PAL} .ep-san-${zeilenKlasse} .ep-warum`);
  await warteBis(async () => (await dlgTitel()) === 'Warum – Herkunft des Werts', 'Warum-Dialog');
  await warteBis(async () => !/Wird geladen/.test((await text('#dialogschicht .warum-inhalt')) ?? 'Wird geladen'), 'Warum geladen');
  return text('#dialogschicht .warum');
};
/** Alle Tabellen der Listenansicht der aktiven Kachel: [{ titel, kopf[], zeilen[[…]], fuss[[…]], zeilenKlassen[] }]. */
const tabellen = () => ausw(`return [...document.querySelectorAll('.kachel.aktiv .li-tabelle-wrap')].map((w) => {
  const t = w.querySelector('table');
  return { titel: w.querySelector('h3')?.textContent ?? '', kopf: [...t.querySelectorAll('thead th')].map((th) => th.childNodes[0].textContent),
    zeilen: [...t.querySelectorAll('tbody tr')].map((tr) => [...tr.children].map((c) => c.textContent)),
    fuss: [...t.querySelectorAll('tfoot tr')].map((tr) => [...tr.children].map((c) => c.textContent)),
    zeilenKlassen: [...t.querySelectorAll('tbody tr')].map((tr) => tr.className), zeilenTitel: [...t.querySelectorAll('tbody tr')].map((tr) => tr.title),
    fehltZellen: [...t.querySelectorAll('td.li-fehlt')].map((c) => c.textContent) };
});`);
const blockZeile = (tabelle, beginntMit) => tabelle.zeilen.find((z) => z[0].startsWith(beginntMit));
const listeWarten = (t) => warteBis(async () => (await anzahl('.kachel.aktiv .li-tabelle')) >= 3, t);

try {
  await gehe('dunkel');
  await ausw(`window.__fehler = []; window.addEventListener('error', (e) => window.__fehler.push(String(e.message))); return 1;`);
  const katalog = (await kern('abfrage', 'katalog', { klasse: 'Waschtisch' }));
  const namenKatalog = katalog.eigenschaften.find((e) => e.name === 'sanitaerArt').auswahlNamen;

  // ================================================================== 1. Apparat: Apparateart und abgeleitete Werte
  abschnitt('1. Eigenschaften eines Apparats (WT-E.02)');
  await waehle('WT-E.02');
  const artSel = `${PAL} select[data-ziel="eig:sanitaerArt"]`;
  pruef(await gibt(artSel), 'Apparateart ist eine Auswahl (Eigenschaft sanitaerArt)');
  const optionen = await ausw(`return [...document.querySelectorAll(${JSON.stringify(artSel + ' option')})].map((o) => [o.value, o.textContent]);`);
  pruef(optionen[0][0] === '' && optionen[0][1] === 'Vorgabe der Klasse (Waschtisch / Wandbecken)', 'erster Eintrag «Vorgabe der Klasse (Waschtisch / Wandbecken)» für nicht gesetzt', JSON.stringify(optionen[0]));
  pruef(optionen.length === 1 + Object.keys(namenKatalog).length, `alle ${Object.keys(namenKatalog).length} Arten des Katalogs stehen in der Auswahl`, String(optionen.length));
  pruef(optionen.slice(1).every(([k, n]) => n === namenKatalog[k]), 'die Auswahl zeigt die Namen (nicht die Schlüssel), gesendet wird der Schlüssel');
  pruef(optionen.some(([k, n]) => k === 'badewanne' && n === 'Badewanne') && optionen.some(([k, n]) => k === 'wc_75' && n === 'Klosettanlage bis 7.5 Liter Spülwassermenge'), 'Beispiele: Badewanne, Klosettanlage bis 7.5 Liter Spülwassermenge');
  pruef((await wert(artSel)) === '', 'Art nicht gesetzt: die Auswahl steht auf der Vorgabe der Klasse');
  pruef(/gilt: Waschtisch \/ Wandbecken \(Vorgabe der Klasse\)/.test(await paletteText()), 'Satz zur wirksamen Art: «gilt: … (Vorgabe der Klasse)»');
  pruef((await wertText('luKw')) === '1 LU' && (await wertText('luWw')) === '1 LU' && (await wertText('du')) === '0.5 DU', 'LU KW 1, LU WW 1, DU 0.5 (abgeleitet)', [await wertText('luKw'), await wertText('luWw'), await wertText('du')].join(' | '));
  pruef(!/\[object Object\]/.test(await paletteText()), 'kein «[object Object]» in der Palette');
  // «?» zeigt die Quelle: Excel, Zeile 8
  const w1 = await warumOeffnen('luKw');
  pruef(/LU_DU Zusammenstellung_\.xlsx/.test(w1) && /Zeile 8/.test(w1), '«?» an LU KW nennt «LU_DU Zusammenstellung_.xlsx, Zeile 8»', w1?.slice(0, 300));
  pruef(/Waschtisch \/ Wandbecken/.test(w1) && /Vorgabe/.test(w1), '«?» nennt Art und Vorgabe der Klasse');
  await zweiBilder('warum_luKw', '#dialogschicht .dlg');
  await alleDialogeZu();
  const w2 = await warumOeffnen('du');
  pruef(/Zeile 8/.test(w2) && /DU/.test(w2), '«?» an DU nennt ebenfalls Zeile 8');
  await alleDialogeZu();
  await zweiBilder('palette_apparat', PAL, 0);

  // Summen vor der Änderung (Leitung am Hausanschluss und Schmutzwasser-Grundleitung)
  const vor = { kw: (await lesen('KW-V1-0')).abgeleitet.summeLuKw, du: (await lesen('SW-G1-1')).abgeleitet.summeDu, sig: await sigmaTexte('AN-SCHEMA-SAN') };
  pruef(vor.kw === 24 && vor.du === 16.7, 'Ausgangslage: KW-V1-0 ΣLU 24, SW-G1-1 ΣDU 16.7', JSON.stringify(vor));
  pruef(vor.sig.includes('ΣLU 24') && vor.sig.includes('ΣDU 16.7'), 'Schema trägt «ΣLU 24» und «ΣDU 16.7»', vor.sig.join(' | '));

  // Art ändern: Badewanne (LU 3/3, DU 0.8)
  await wahl(artSel, 'badewanne');
  await warteBis(async () => (await wertText('luKw')) === '3 LU', 'LU KW 3 nach der Änderung');
  pruef((await wertText('luKw')) === '3 LU' && (await wertText('luWw')) === '3 LU' && (await wertText('du')) === '0.8 DU', 'Art Badewanne: LU KW 3, LU WW 3, DU 0.8 (live)', [await wertText('luKw'), await wertText('luWw'), await wertText('du')].join(' | '));
  pruef(/gilt: Badewanne \(am Apparat gesetzt\)/.test(await paletteText()), 'Satz: «gilt: Badewanne (am Apparat gesetzt)»');
  const el = await lesen('WT-E.02');
  pruef(el.eigenschaften.sanitaerArt?.wert === 'badewanne', 'der Schlüssel «badewanne» ist gespeichert (nicht der Name)', JSON.stringify(el.eigenschaften.sanitaerArt));
  const nach = { kw: (await lesen('KW-V1-0')).abgeleitet.summeLuKw, du: (await lesen('SW-G1-1')).abgeleitet.summeDu };
  pruef(nach.kw === 26 && Math.abs(nach.du - 17.0) < 1e-9, 'Summen der Leitungen ändern sich: KW-V1-0 ΣLU 26, SW-G1-1 ΣDU 17', JSON.stringify(nach));
  await warteBis(async () => (await sigmaTexte('AN-SCHEMA-SAN')).includes('ΣLU 26'), 'Schema zeigt ΣLU 26');
  pruef((await sigmaTexte('AN-SCHEMA-SAN')).includes('ΣDU 17'), 'Schema zeigt «ΣDU 17»');
  const w3 = await warumOeffnen('luKw');
  pruef(/Zeile 17/.test(w3) && /Badewanne/.test(w3), '«?» zeigt jetzt Excel-Zeile 17 (Badewanne)', w3?.slice(0, 200));
  await alleDialogeZu();
  // zurück zur Vorgabe
  await wahl(artSel, '');
  await warteBis(async () => (await wertText('luKw')) === '1 LU', 'zurück zu LU KW 1');
  pruef((await lesen('KW-V1-0')).abgeleitet.summeLuKw === 24, 'Zurück zur Vorgabe der Klasse: ΣLU wieder 24');
  pruef((await lesen('WT-E.02')).eigenschaften.sanitaerArt === undefined, 'Eigenschaft sanitaerArt ist wieder nicht gesetzt (null)');
  // Bodenablauf ohne Vorgabe
  await waehle('BA-U.02');
  const baOpt = await ausw(`return document.querySelector(${JSON.stringify(artSel)}).selectedOptions[0].textContent;`);
  pruef(/Bodenablauf DN 70/.test(baOpt), 'Bodenablauf BA-U.02 (Art im Beispiel gesetzt): Auswahl zeigt «Bodenablauf DN 70»', baOpt);
  pruef(/Vorgabe der Klasse: keine – Art wählen/.test(await ausw(`return document.querySelector(${JSON.stringify(artSel)}).options[0].textContent;`)), 'Bodenablauf hat keine Vorgabe: erster Eintrag «Vorgabe der Klasse: keine – Art wählen»');
  pruef((await wertText('luKw')) === null && (await wertText('du')) === '1.5 DU', 'Bodenablauf: nur DU 1.5, kein LU (die Art hat keinen Wert)', [await wertText('luKw'), await wertText('du')].join(' | '));

  // ================================================================== 2. Leitung
  abschnitt('2. Eigenschaften einer Leitung (KW-V1-0, SW-G1-1)');
  await waehle('KW-V1-0');
  pruef((await wertText('summeLuKw')) === '24 LU' && (await wertText('summeLuWw')) === '0 LU' && (await wertText('groessterLu')) === '3 LU', 'KW-V1-0: ΣLU KW 24, ΣLU WW 0, grösster Einzel-LU 3', [await wertText('summeLuKw'), await wertText('summeLuWw'), await wertText('groessterLu')].join(' | '));
  pruef(/15 Apparate/.test(await paletteText()), 'Zeile «15 Apparate (Apparate stromab)»');
  const w4 = await warumOeffnen('summeLuKw');
  pruef(/Rechenweg/.test(w4) && /ΣLU KW = Summe der LU KW/.test(w4), '«?» an ΣLU KW zeigt den Rechenweg');
  pruef(/Beiträge der Apparate/.test(w4) && (await anzahl('#dialogschicht .warum table.tab tbody tr')) >= 15, '«?» listet die Beiträge der Apparate als Tabelle (15 Zeilen)', String(await anzahl('#dialogschicht .warum table.tab tbody tr')));
  const bwZeile = await ausw(`return [...document.querySelectorAll('#dialogschicht .warum table.tab tbody tr')].map((r) => [...r.children].map((c) => c.textContent)).find((z) => z[0] === 'BW-1.02') ?? null;`);
  pruef(/LU_DU Zusammenstellung_\.xlsx/.test(w4) && bwZeile?.[1] === 'Badewanne' && bwZeile?.[5] === '17', '«?» nennt die Quelle (Excel) und je Apparat die Excel-Zeile (BW-1.02: Badewanne, Zeile 17)', JSON.stringify(bwZeile));
  await zweiBilder('warum_summe', '#dialogschicht .dlg');
  await alleDialogeZu();
  await waehle('SW-G1-1');
  pruef((await wertText('summeDu')) === '16.7 DU' && (await wertText('summeLuKw')) === null, 'SW-G1-1: ΣDU 16.7, keine LU-Zeilen', await wertText('summeDu'));
  await zweiBilder('palette_leitung', PAL, 0);

  // ================================================================== 3. Anlage: K und Spezialinstallationen
  abschnitt('3. Eigenschaften der Anlage A-SAN: Abflusskennzahl K, Spezialinstallationen');
  await waehle('A-SAN');
  const kSel = `${PAL} [data-ziel="eig:abflusskennzahl"]`;
  pruef((await wert(kSel)) === '0.5', 'K ist eine freie Zahl (Wert 0.5 aus dem Beispiel)', await wert(kSel));
  pruef((await anzahl(`${PAL} select[data-ziel="eig:abflusskennzahl"]`)) === 0, 'keine Auswahlliste für K');
  pruef((await text(`${PAL} .ep-k-hinweis .ep-text`)) === 'Wert aus SN 592000 nach Gebäudeart – Norm liegt nicht vor; Walkringen: 0.5', 'Hinweis unter K: «Wert aus SN 592000 nach Gebäudeart – Norm liegt nicht vor; Walkringen: 0.5»', await text(`${PAL} .ep-k-hinweis .ep-text`));
  const spez = `${PAL} .ep-spezial`;
  pruef(await gibt(spez), 'Spezialinstallationen als Tabelle (statt Kommaliste)');
  pruef((await anzahl(`${spez} thead th`)) === 4 && /Bezeichnung/.test(await text(`${spez} thead`)) && /Versorgung l\/s/.test(await text(`${spez} thead`)) && /Entsorgung l\/s/.test(await text(`${spez} thead`)), 'Kopf: Bezeichnung, Versorgung l/s, Entsorgung l/s');
  pruef((await anzahl(`${spez} tbody tr`)) === 0, 'noch keine Zeile');
  await zweiBilder('palette_anlage', PAL, 0);
  const spitze0 = (await zusammenstellung()).spitzeGesamt;
  // Zeile hinzufügen: erst die Bezeichnung – die unvollständige Zeile wird nicht gesendet
  await klick(`${spez} .ep-spez-neu`);
  pruef((await anzahl(`${spez} tbody tr`)) === 1, 'Zeile hinzufügen');
  await tippe(`${spez} tbody tr:last-child .sp-b`, 'Garteneinrichtung');
  pruef(!(await ausw(`return document.querySelector(${JSON.stringify(spez + ' .ep-spez-hinweis')}).hidden;`)), 'Bezeichnung ohne Zahl: Hinweis an der Tabelle, noch nichts gesendet');
  pruef(((await lesen('A-SAN')).eigenschaften.spezialinstallationen) === undefined, 'der Kern hat noch keine Spezialinstallation');
  await tippe(`${spez} tbody tr:last-child .sp-v`, '0,5');
  await warteBis(async () => Array.isArray((await lesen('A-SAN')).eigenschaften.spezialinstallationen?.wert), 'Spezialinstallation beim Kern');
  const si = (await lesen('A-SAN')).eigenschaften.spezialinstallationen.wert;
  pruef(JSON.stringify(si) === JSON.stringify([{ bezeichnung: 'Garteneinrichtung', versorgung: 0.5 }]), 'gesendet wird die Liste { bezeichnung, versorgung } (Komma «0,5» als Zahl 0.5)', JSON.stringify(si));
  const z1 = await zusammenstellung();
  pruef(Math.abs(z1.spitzeGesamt - (spitze0 + 0.5)) < 1e-9, 'Spitze gesamt wächst um die Versorgung 0.5 l/s', `${spitze0} → ${z1.spitzeGesamt}`);
  await warteBis(async () => (await anzahl(`${spez} tbody tr`)) === 1 && (await wert(`${spez} tbody tr .sp-b`)) === 'Garteneinrichtung', 'Palette zeigt die Zeile aus dem Modell');
  await tippe(`${spez} tbody tr:last-child .sp-e`, '0.25');
  await warteBis(async () => (await lesen('A-SAN')).eigenschaften.spezialinstallationen.wert[0].entsorgung === 0.25, 'Entsorgung 0.25');
  pruef(true, 'Entsorgung 0.25 l/s ergänzt');
  pruef(!/\[object Object\]/.test(await paletteText()), 'kein «[object Object]» auch mit Eintrag');
  await zweiBilder('palette_spezial', PAL, 0);
  // negative Zahl lehnt der Kern ab
  await tippe(`${spez} tbody tr:last-child .sp-v`, '-1');
  await warteBis(() => gibt(`${PAL} .ep-zeile.fehlerhaft`), 'Fehler am Feld');
  pruef(/Zahl ≥ 0|≥ 0/.test((await text(`${PAL} .ep-zeile.fehlerhaft .ep-fehler`)) ?? ''), 'negative Versorgung: der Kern lehnt ab, die Meldung steht am Feld', await text(`${PAL} .ep-zeile.fehlerhaft .ep-fehler`));
  await esc();   // Eingabe verwerfen, Palette neu zeichnen
  await warteBis(async () => (await wert(`${spez} tbody tr .sp-v`)) === '0.5', 'Palette wieder auf dem Stand des Modells');
  // Zeile löschen
  await klick(`${spez} tbody tr:last-child .ep-spez-weg`);
  await warteBis(async () => (await lesen('A-SAN')).eigenschaften.spezialinstallationen?.wert === null || (await lesen('A-SAN')).eigenschaften.spezialinstallationen === undefined || (await lesen('A-SAN')).eigenschaften.spezialinstallationen?.wert === undefined, 'Zeile gelöscht');
  pruef((await zusammenstellung()).spitzeGesamt === spitze0, 'Spezialinstallation gelöscht: Spitze gesamt wie vorher');
  pruef((await anzahl(`${spez} tbody tr`)) === 0, 'Tabelle leer');

  // ================================================================== 4. Strangschema
  abschnitt('4. Strangschema: Beschriftung «ΣLU …» und «ΣDU …», Schalter belastung');
  await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-SCHEMA-SAN'); return 1;`);
  await warteBis(async () => (await svgSigma()).length >= 10, 'Schema gezeichnet', 20000);
  await warte(500);
  const sigma = await svgSigma();
  pruef(sigma.includes('ΣLU 24') && sigma.includes('ΣDU 16.7'), 'im Schema stehen «ΣLU 24» und «ΣDU 16.7»', [...new Set(sigma)].join(' | '));
  pruef(sigma.every((t) => /^Σ(LU \d+(\.\d)?( \/ \d+(\.\d)?)?|DU \d+(\.\d)?)$/.test(t)), 'alle Texte haben die Form «ΣLU 12», «ΣLU 12 / 8» oder «ΣDU 4.6»', sigma.filter((t) => !/^Σ(LU \d+(\.\d)?( \/ \d+(\.\d)?)?|DU \d+(\.\d)?)$/.test(t)).join(' | '));
  const pg = await plangrafik('AN-SCHEMA-SAN');
  const alleTexte = pg.teile.flatMap((t) => t.primitive.filter((p) => p.art === 'text' && /^(Σ|NW|PE)/.test(p.text ?? '')));
  const rahmen = (p) => { const b = 0.6 * (p.groesse ?? 1.6) * p.text.length, h = 0.75 * (p.groesse ?? 1.6), [x, y] = p.punkte[0]; return p.winkel === 90 ? [x - h, y - b / 2, x, y + b / 2] : [x - b / 2, y, x + b / 2, y + h]; };
  let ueberdeckt = 0;
  for (let i = 0; i < alleTexte.length; i++) for (let j = i + 1; j < alleTexte.length; j++) {
    const a = rahmen(alleTexte[i]), b = rahmen(alleTexte[j]);
    if (a[0] < b[2] && b[0] < a[2] && a[1] < b[3] && b[1] < a[3]) ueberdeckt++;
  }
  pruef(alleTexte.length > 30 && ueberdeckt === 0, `kein Belastungs- oder Grössentext überdeckt einen anderen (${alleTexte.length} Texte)`, `${ueberdeckt} Überdeckungen`);
  await zweiBilder('schema_belastung', '.kachel.aktiv', 0);
  const wt102 = await ausw(`const z = await import('/js/zustand.js'); return z.element('WT-1.02').id;`);
  await detailBilder('schema_detail', `.kachel.aktiv svg g[data-id="${wt102}"]`, 110, 3);
  // Schalter in der Palette der Ansicht
  await waehleAnsicht('AN-SCHEMA-SAN');
  const bSel = `${PAL} [data-ziel="eig:belastung"]`;
  pruef(await gibt(bSel) && (await ausw(`return document.querySelector(${JSON.stringify(bSel)}).checked;`)), 'Palette der Ansicht: «belastung» ist eingeschaltet (Vorgabe ein)');
  await klick(bSel);
  await warteBis(async () => (await svgSigma()).length === 0, 'Beschriftung aus', 15000);
  pruef((await svgSigma()).length === 0 && (await sigmaTexte('AN-SCHEMA-SAN')).length === 0, 'Schalter aus: keine Belastungstexte mehr (Schema und Plangrafik)');
  pruef((await ausw(`return [...document.querySelectorAll('.kachel.aktiv svg text')].filter((t) => /^NW/.test(t.textContent)).length;`)) > 5, 'die Rohrgrössen («NW …») bleiben');
  pruef((await lesen('AN-SCHEMA-SAN')).eigenschaften.belastung?.wert === false, 'Eigenschaft belastung = false am Kern');
  await zweiBilder('schema_ohne_belastung', '.kachel.aktiv', 0);
  await klick(bSel);
  await warteBis(async () => (await svgSigma()).length >= 10, 'Beschriftung wieder ein', 15000);
  pruef((await svgSigma()).includes('ΣLU 24'), 'Schalter ein: «ΣLU 24» ist wieder da');

  // ================================================================== 5. Liste
  abschnitt('5. Liste «Sanitär-Zusammenstellung (W3)»');
  await registerWahl('Ansicht');
  await klick(knopfSel('ansicht.liste'));
  await warteBis(async () => (await dlgTitel()) === 'Liste anlegen', 'Dialog Liste anlegen');
  const listenOpt = await ausw(`return [...document.querySelectorAll('#dialogschicht .f-liste option')].map((o) => [o.value, o.textContent]);`);
  pruef(listenOpt.some(([k, n]) => k === 'sanitaer_zusammenstellung' && n === 'Sanitär-Zusammenstellung (W3)'), 'im Dialog «Liste anlegen» steht «Sanitär-Zusammenstellung (W3)»', JSON.stringify(listenOpt));
  pruef(await ausw(`return document.querySelector('#dialogschicht .f-anlage-zeile').hidden;`), 'die Zeile «Anlage» ist bei anderen Listen verborgen');
  await wahl('#dialogschicht .f-liste', 'sanitaer_zusammenstellung');
  pruef(!(await ausw(`return document.querySelector('#dialogschicht .f-anlage-zeile').hidden;`)), 'bei der Sanitär-Zusammenstellung erscheint die Zeile «Anlage»');
  pruef((await wert('#dialogschicht .f-anlage')) === 'A-SAN' && (await wert('#dialogschicht .f-kz')) === 'AN-LI-SANITAER-A-SAN' && (await wert('#dialogschicht .f-name')) === 'Sanitär-Zusammenstellung (W3) – A-SAN', 'Vorschläge für Kennzeichen und Name', `${await wert('#dialogschicht .f-kz')} | ${await wert('#dialogschicht .f-name')}`);
  await zweiBilder('dialog_liste_anlegen', '#dialogschicht .dlg');
  await dlgKnopf('Anlegen');
  await listeWarten('Liste geladen');
  await warte(400);
  const ansichtKz = await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); return z.element(f.aktive()?.id)?.kennzeichen ?? null;`);
  pruef(ansichtKz === 'AN-LI-SANITAER-A-SAN', 'die Liste ist angelegt und geöffnet', String(ansichtKz));
  const lw = (await lesen('AN-LI-SANITAER-A-SAN')).eigenschaften;
  pruef(lw.liste?.wert === 'sanitaer_zusammenstellung' && lw.anlage?.wert === 'A-SAN' && lw.art?.wert === 'liste', 'Ansicht art liste, liste sanitaer_zusammenstellung, anlage A-SAN', JSON.stringify(lw));
  const tk = await text('.kachel.aktiv .li-titel');
  pruef(/^Sanitär-Zusammenstellung \(W3\) · A-SAN/.test(tk ?? ''), 'Titel «Sanitär-Zusammenstellung (W3) · A-SAN …»', tk);
  const kopfTexte = await ausw(`return [...document.querySelectorAll('.kachel.aktiv .li-summe')].map((e) => e.textContent);`);
  pruef(kopfTexte.some((t) => /Quelle: LU_DU Zusammenstellung_\.xlsx/.test(t)) && kopfTexte.some((t) => /16 Apparate/.test(t)), 'Kopf nennt Anlage, 16 Apparate und die Quelle (Excel)', kopfTexte.join(' | '));
  let tb = await tabellen();
  pruef(tb.length === 3 && tb[0].titel.startsWith('Apparate') && tb[1].titel === 'Trinkwasser' && tb[2].titel === 'Entwässerung', 'drei Tabellen: Apparate, Trinkwasser, Entwässerung', tb.map((t) => t.titel).join(' | '));
  const t0 = tb[0];
  pruef(JSON.stringify(t0.kopf) === JSON.stringify(['Apparateart', 'LU KW', 'LU WW', 'DU', 'UG', 'EG', '1.OG', 'Anzahl', 'ΣLU KW', 'ΣLU WW', 'ΣDU']), 'Spalten wie das Excel: Apparateart, LU KW, LU WW, DU, ein Geschoss je Spalte, Anzahl, ΣLU KW, ΣLU WW, ΣDU', JSON.stringify(t0.kopf));
  const wt = t0.zeilen.find((z) => z[0] === 'Waschtisch / Wandbecken');
  pruef(JSON.stringify(wt) === JSON.stringify(['Waschtisch / Wandbecken', '1', '1', '0,5', '', '2', '2', '4', '4', '4', '2,0']), 'Zeile Waschtisch: 1 / 1 / 0,5, EG 2, 1.OG 2, Anzahl 4, ΣLU 4 / 4, ΣDU 2,0', JSON.stringify(wt));
  const wc = t0.zeilen.find((z) => z[0].startsWith('Klosettanlage bis 7.5'));
  pruef(JSON.stringify(wc) === JSON.stringify([wc?.[0], '1', '', '2,0', '', '2', '2', '4', '4', '', '8,0']), 'Zeile Klosett: kein LU WW (leere Zelle wie im Excel), ΣDU 8,0', JSON.stringify(wc));
  pruef(JSON.stringify(t0.zeilen.map((z) => z[0])) === JSON.stringify(['Waschtisch / Wandbecken', 'Dusche nicht staubar', 'Badewanne', 'Spülbecken 1- und 2-teilig', 'Waschmaschine bis 6 kg', 'Bodenablauf DN 70', wc?.[0]]), 'Zeilen in der Reihenfolge des Excel-Katalogs', JSON.stringify(t0.zeilen.map((z) => z[0])));
  pruef(JSON.stringify(t0.fuss[0]) === JSON.stringify(['Total', '', '', '', '', '', '', '16', '24', '18', '16,7']), 'Totalzeile: 16 Apparate, ΣLU KW 24, ΣLU WW 18, ΣDU 16,7', JSON.stringify(t0.fuss[0]));
  const tw = tb[1];
  pruef(blockZeile(tw, 'Total LU')?.[1] === '42' && blockZeile(tw, 'Total LU')?.[2] === 'LU', 'Trinkwasser: Total LU 42', JSON.stringify(blockZeile(tw, 'Total LU')));
  pruef(blockZeile(tw, 'Volumenstrom')?.[1] === '4,20' && blockZeile(tw, 'Volumenstrom')?.[2] === 'l/s', 'Volumenstrom 4,20 l/s', JSON.stringify(blockZeile(tw, 'Volumenstrom')));
  const k3 = blockZeile(tw, 'Spitzendurchfluss Kurve «grösster LU 3»'), k5 = blockZeile(tw, 'Spitzendurchfluss Kurve «grösster LU 5»');
  pruef(k3?.[0].endsWith('– massgebend') && k3?.[1] === '0,762' && k5?.[0].endsWith('(nicht massgebend)') && k5?.[1] === '0,865', 'Kurve 3 massgebend (0,762 l/s), Kurve 5 nicht massgebend (0,865 l/s)', JSON.stringify([k3, k5]));
  const i3 = tw.zeilen.indexOf(k3), i5 = tw.zeilen.indexOf(k5);
  pruef(tw.zeilenKlassen[i3] === 'li-massgebend' && tw.zeilenKlassen[i5] === 'li-nebenwert', 'massgebende Kurve fett, die andere klein (Klassen li-massgebend / li-nebenwert)', `${tw.zeilenKlassen[i3]} | ${tw.zeilenKlassen[i5]}`);
  pruef(/B55 = M43\+N43/.test(tw.zeilenTitel[tw.zeilen.indexOf(blockZeile(tw, 'Total LU'))]) && /B56 = B55\*0\.1/.test(tw.zeilenTitel[tw.zeilen.indexOf(blockZeile(tw, 'Volumenstrom'))]), 'Tooltips nennen Formel und Excel-Zelle (B55 = M43+N43, B56 = B55*0.1)');
  pruef(/C57 = B56\^0\.353\*0\.459/.test(tw.zeilenTitel[i3]) && /genau: 0,76176/.test(tw.zeilenTitel[i3]), 'Tooltip der Kurve 3: C57 = B56^0.353*0.459 und der genaue Wert', tw.zeilenTitel[i3]);
  pruef(blockZeile(tw, 'Spezialinstallationen Versorgung gesamt')?.[1] === '0', 'Spezialinstallationen Versorgung gesamt: 0', JSON.stringify(blockZeile(tw, 'Spezialinstallationen Versorgung gesamt')));
  const sg = blockZeile(tw, 'Spitzendurchfluss gesamt (Kurve');
  pruef(sg?.[1] === '0,762', 'Spitzendurchfluss gesamt 0,762 l/s', JSON.stringify(sg));
  const te = tb[2];
  pruef(blockZeile(te, 'ΣDU')?.[1] === '16,7' && blockZeile(te, 'Abflusskennzahl K')?.[1] === '0,5', 'Entwässerung: ΣDU 16,7, K 0,5', JSON.stringify([blockZeile(te, 'ΣDU'), blockZeile(te, 'Abflusskennzahl K')]));
  pruef(blockZeile(te, 'Schmutzwasserabfluss Q_WW')?.[1] === '2,04' && blockZeile(te, 'Gesamtabfluss Q_tot')?.[1] === '2,04', 'Q_WW 2,04 l/s (0,5 × √16,7), Q_tot 2,04 l/s', JSON.stringify([blockZeile(te, 'Schmutzwasserabfluss Q_WW'), blockZeile(te, 'Gesamtabfluss Q_tot')]));
  pruef(/M66 = M65\*\(\(M64\)\^\(1\/2\)\)/.test(te.zeilenTitel[te.zeilen.indexOf(blockZeile(te, 'Schmutzwasserabfluss Q_WW'))]), 'Tooltip Q_WW: M66 = M65*((M64)^(1/2))');
  pruef(!/\[object Object\]|NaN|undefined/.test(await text('.kachel.aktiv .li-inhalt')), 'kein «[object Object]», «NaN» oder «undefined» in der Liste');
  await zweiBilder('liste', '.kachel.aktiv', 0);

  // Spezialinstallation hinzufügen ändert die Spitze gesamt (live in der geöffneten Liste)
  await befehl('element_aendern', { element: 'A-SAN', eigenschaften: { spezialinstallationen: [{ bezeichnung: 'Garten', versorgung: 0.5, entsorgung: 0.25 }] } });
  await warteBis(async () => blockZeile((await tabellen())[1], 'Spezialinstallation: Garten') !== undefined, 'Liste zeigt die Spezialinstallation', 15000);
  tb = await tabellen();
  const sg2 = blockZeile(tb[1], 'Spitzendurchfluss gesamt (Kurve');
  pruef(blockZeile(tb[1], 'Spezialinstallation: Garten')?.[1] === '0,5', 'Zeile «Spezialinstallation: Garten» 0,5 l/s im Block Trinkwasser (wie eingetragen)', JSON.stringify(blockZeile(tb[1], 'Spezialinstallation: Garten')));
  pruef(sg2?.[1] === '1,26', 'Spitzendurchfluss gesamt wächst von 0,762 auf 1,26 l/s (+ 0,5)', JSON.stringify(sg2));
  const qt2 = blockZeile(tb[2], 'Gesamtabfluss Q_tot');
  pruef(blockZeile(tb[2], 'Spezialinstallation: Garten')?.[1] === '0,25' && qt2?.[1] === '2,29', 'Block Entwässerung: Garten 0,25, Q_tot 2,29 l/s', JSON.stringify([blockZeile(tb[2], 'Spezialinstallation: Garten'), qt2]));
  await zweiBilder('liste_spezial', '.kachel.aktiv', 0);

  // CSV mit beiden Blöcken
  await klick('.kachel.aktiv .li-csv');
  await warteBis(async () => (await dlgTitel()) === 'Liste als CSV', 'CSV-Dialog');
  const vorgabeName = await wert('#dialogschicht .csv-name');
  pruef(/^Sanitär-Zusammenstellung_\(W3\)_A-SAN_.*_\d{4}-\d{2}-\d{2}\.csv$/.test(vorgabeName), 'Dateiname-Vorschlag: Titel_Anlage_Datum.csv', vorgabeName);
  await tippe('#dialogschicht .csv-name', 'm4_sanitaer.csv', false);
  await dlgKnopf('Erzeugen');
  const f1 = await dateiWarten('m4_sanitaer.csv');
  pruef(!!f1, 'CSV-Datei ist im Download-Ordner angekommen');
  const csv = f1 ? readFileSync(f1) : Buffer.alloc(0);
  const csvText = csv.subarray(3).toString('utf8');
  pruef(csv.subarray(0, 3).toString('hex') === 'efbbbf', 'CSV: UTF-8 mit BOM');
  const zeilen = csvText.split('\r\n');
  pruef(zeilen.includes('Apparate (LU und DU je Stück, Stück je Geschoss, Summen)') && zeilen.includes('Trinkwasser') && zeilen.includes('Entwässerung'), 'CSV: drei Tabellen mit Titelzeilen (Apparate, Trinkwasser, Entwässerung)');
  pruef(zeilen.includes('Apparateart;LU KW;LU WW;DU;UG;EG;1.OG;Anzahl;ΣLU KW;ΣLU WW;ΣDU'), 'CSV: Kopfzeile der Apparate-Tabelle', zeilen[1]);
  pruef(zeilen.includes('Waschtisch / Wandbecken;1;1;0,5;;2;2;4;4;4;2,0') && zeilen.includes('Total;;;;;;;16;24;18;16,7'), 'CSV: Zeile Waschtisch und Totalzeile (gerundet wie in der Ansicht, Dezimalkomma)');
  pruef(zeilen.includes('Grösse;Wert;Einheit;Formel (Excel-Zelle)'), 'CSV: die Blöcke tragen die Spalte «Formel (Excel-Zelle)»');
  pruef(zeilen.some((z) => z.startsWith('Total LU;42;LU;ΣLU KW + ΣLU WW (B55 = M43+N43)')), 'CSV: Total LU 42 mit Formel und Excel-Zelle', zeilen.find((z) => z.startsWith('Total LU')));
  pruef(zeilen.some((z) => z.startsWith('Gesamtabfluss Q_tot;2,29;l/s;Q_tot = Q_WW + Spezialinstallationen Entsorgung (M67 = M66+N51)')), 'CSV: Q_tot 2,29 l/s mit Formel (M67 = M66+N51)', zeilen.find((z) => z.startsWith('Gesamtabfluss')));
  pruef(zeilen.some((z) => z.startsWith('Spezialinstallation: Garten;0,5;l/s')) && zeilen.some((z) => z.startsWith('Spezialinstallation: Garten;0,25;l/s')), 'CSV: Spezialinstallation in beiden Blöcken');
  pruef(!/\[object Object\]|NaN|undefined/.test(csvText), 'CSV: kein «[object Object]», «NaN» oder «undefined»');
  await befehl('element_aendern', { element: 'A-SAN', eigenschaften: { spezialinstallationen: null } });
  await warteBis(async () => blockZeile((await tabellen())[1], 'Spezialinstallation: Garten') === undefined, 'Spezialinstallation wieder weg', 15000);

  // K leer → «K fehlt» und Befund P25
  const kVorher = (await lesen('A-SAN')).eigenschaften.abflusskennzahl;
  await waehle('A-SAN');
  await tippe(kSel, '');
  await warteBis(async () => (await lesen('A-SAN')).eigenschaften.abflusskennzahl === undefined, 'K beim Kern gelöscht');
  await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-LI-SANITAER-A-SAN'); return 1;`);
  await warteBis(async () => blockZeile((await tabellen())[2], 'Abflusskennzahl K')?.[1] === 'K fehlt', 'Liste zeigt «K fehlt»', 15000);
  tb = await tabellen();
  const ohneK = [blockZeile(tb[2], 'Abflusskennzahl K'), blockZeile(tb[2], 'Schmutzwasserabfluss Q_WW'), blockZeile(tb[2], 'Gesamtabfluss Q_tot')];
  pruef(ohneK.every((z) => z?.[1] === 'K fehlt'), 'ohne K steht «K fehlt» statt K, Q_WW und Q_tot (keine Zahl)', JSON.stringify(ohneK));
  pruef(tb[2].fehltZellen.length === 3, 'die drei Zellen sind als Fehler markiert (li-fehlt)', String(tb[2].fehltZellen.length));
  pruef(blockZeile(tb[2], 'ΣDU')?.[1] === '16,7', 'ΣDU bleibt (16,7)');
  const hinweisKopf = await ausw(`return [...document.querySelectorAll('.kachel.aktiv .li-summe')].map((e) => e.textContent).join(' | ');`);
  pruef(/Abflusskennzahl K fehlt/.test(hinweisKopf), 'der Hinweis des Kerns «Abflusskennzahl K fehlt …» steht im Kopf der Liste', hinweisKopf);
  await zweiBilder('liste_k_fehlt', '.kachel.aktiv', 0);
  // csv ohne K
  await klick('.kachel.aktiv .li-csv');
  await warteBis(async () => (await dlgTitel()) === 'Liste als CSV', 'CSV-Dialog');
  await tippe('#dialogschicht .csv-name', 'm4_sanitaer_ohne_k.csv', false);
  await dlgKnopf('Erzeugen');
  const f2 = await dateiWarten('m4_sanitaer_ohne_k.csv');
  pruef(!!f2 && readFileSync(f2).toString('utf8').split('\r\n').some((z) => z.startsWith('Gesamtabfluss Q_tot;K fehlt;')), 'CSV ohne K: «Gesamtabfluss Q_tot;K fehlt;»');
  // Befund P25 im Dialog Prüfbefunde
  await registerWahl('Verwalten');
  await klick(knopfSel('verwalten.befunde'));
  await warteBis(async () => (await dlgTitel()) === 'Prüfbefunde', 'Dialog Prüfbefunde');
  await warteBis(async () => (await anzahl('#dialogschicht .b-liste tbody tr')) > 0, 'Befunde geladen');
  const befZeilen = await ausw(`return [...document.querySelectorAll('#dialogschicht .b-liste tbody tr')].map((r) => [...r.children].map((c) => c.textContent));`);
  const p25 = befZeilen.find((z) => z[1] === 'P25');
  pruef(!!p25 && p25[2] === 'A-SAN' && /Abflusskennzahl/.test(p25[3]), 'Befund P25 «Abflusskennzahl fehlt» an A-SAN erscheint im Dialog Prüfbefunde', JSON.stringify(p25));
  pruef(/P25[^·]*: 1/.test(await text('#dialogschicht .b-zahl')), 'die Zusammenfassung zählt P25: 1', await text('#dialogschicht .b-zahl'));
  await zweiBilder('befunde_p25', '#dialogschicht .dlg');
  await alleDialogeZu();
  // K wieder setzen (über die Palette)
  await waehle('A-SAN');
  await tippe(kSel, '0,5');
  await warteBis(async () => (await lesen('A-SAN')).eigenschaften.abflusskennzahl?.wert === 0.5, 'K wieder 0.5');
  pruef(!(await kern('abfrage', 'pruefen', {})).befunde.some((b) => b.code === 'P25'), 'mit K 0,5 ist der Befund P25 weg');

  // Art-Änderung wirkt auf die offene Liste
  await befehl('element_aendern', { element: 'WT-E.02', eigenschaften: { sanitaerArt: 'badewanne' } });
  await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-LI-SANITAER-A-SAN'); return 1;`);
  await warteBis(async () => (await tabellen())[0].fuss[0]?.[8] === '26', 'Liste: ΣLU KW 26 nach der Art-Änderung', 15000);
  tb = await tabellen();
  pruef(tb[0].zeilen.find((z) => z[0] === 'Badewanne')?.[7] === '3' && tb[0].zeilen.find((z) => z[0] === 'Waschtisch / Wandbecken')?.[7] === '3', 'Zeilen: 3 Badewannen, 3 Waschtische', JSON.stringify(tb[0].zeilen.map((z) => [z[0], z[7]])));
  await befehl('element_aendern', { element: 'WT-E.02', eigenschaften: { sanitaerArt: null } });
  await warteBis(async () => (await tabellen())[0].fuss[0]?.[8] === '24', 'Liste: zurück zu ΣLU KW 24', 15000);

  // ================================================================== 6. Knopf, Planblätter
  abschnitt('6. Knopf «Sanitär-Zusammenstellung», Planblätter');
  await registerWahl('Gebäudetechnik');
  const k = knopfSel('sanitaer.zusammenstellung');
  pruef(await gibt(k), 'Knopf «Sanitär-Zusammenstellung» im Register Gebäudetechnik');
  const gruppe = await ausw(`return document.querySelector(${JSON.stringify(k)})?.closest('.mb-gruppe')?.dataset.gruppe ?? null;`);
  pruef(gruppe === 'Apparate und Armaturen', 'in der Gruppe «Apparate und Armaturen» (keine neue Gruppe, kein neues Register)', String(gruppe));
  pruef(!(await ausw(`return document.querySelector(${JSON.stringify(k)}).disabled || document.querySelector(${JSON.stringify(k)}).classList.contains('gesperrt');`)), 'Knopf ist bedienbar');
  // Bilder des Menübands: Gebäudetechnik mit dem neuen Knopf, Strich- und Revit-Symbole, beide Designs
  const bandClip = { x: 0, y: 0, width: 1920, height: 150 };
  for (const stil of ['strich', 'revit']) {
    await ausw(`const m = await import('/js/oberflaeche/symbolstil-umschalten.js'); await m.symbolstilWaehlen(${JSON.stringify(stil)}, false); return 1;`);
    await warte(300);
    for (const d of ['dunkel', 'hell']) { await designSetzen(d); bilder.push(await speichern(`m4_menueband_gebaeudetechnik_${stil}_${d}`, bandClip)); }
    if (stil === 'revit') {
      const svg = await ausw(`return document.querySelector(${JSON.stringify(k + ' .mb-sym svg')})?.getAttribute('class') ?? '';`);
      pruef(/sym-rv/.test(svg), 'Revit-Satz: der Knopf trägt die eigene Revit-Zeichnung (32 px)', svg);
    }
  }
  await ausw(`const m = await import('/js/oberflaeche/symbolstil-umschalten.js'); await m.symbolstilWaehlen('strich', false); return 1;`);
  await designSetzen('dunkel');
  const symSvg = await ausw(`return document.querySelector(${JSON.stringify(k + ' .mb-sym svg')})?.outerHTML ?? '';`);
  pruef(symSvg.startsWith('<svg') && !/sym-rv/.test(symSvg) && symSvg.length > 200, 'eigenes Symbol (Strich)', symSvg.slice(0, 80));
  const nListen0 = await ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Ansicht').filter((a) => a.werte?.liste === 'sanitaer_zusammenstellung').length;`);
  await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); z.auswaehlen(z.element('WT-E.02').id); return 1;`);   // Auswahl: Apparat der Anlage
  await ausw(`const f = await import('/js/fenster.js'); f.alle().forEach((o) => f.schliessen?.(o.id)); return 1;`).catch(() => {});
  await klick(k);
  await warteBis(async () => (await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); return z.element(f.aktive()?.id)?.kennzeichen ?? null;`)) === 'AN-LI-SANITAER-A-SAN', 'Knopf öffnet die Liste', 15000);
  const nListen1 = await ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Ansicht').filter((a) => a.werte?.liste === 'sanitaer_zusammenstellung').length;`);
  pruef(nListen0 === 1 && nListen1 === 1, 'der Knopf öffnet die vorhandene Liste der Anlage und legt keine zweite an', `${nListen0} → ${nListen1}`);
  // ohne Liste: der Knopf legt sie an
  await ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(null); return 1;`);
  await befehl('element_loeschen', { element: 'AN-LI-SANITAER-A-SAN' });
  await warteBis(async () => (await ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Ansicht').filter((a) => a.werte?.liste === 'sanitaer_zusammenstellung').length;`)) === 0, 'Liste gelöscht');
  await klick(k);
  await warteBis(async () => (await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); return z.element(f.aktive()?.id)?.kennzeichen ?? null;`)) === 'AN-LI-SANITAER-A-SAN', 'Knopf legt die Liste an und öffnet sie', 15000);
  await listeWarten('Liste geladen');
  pruef((await tabellen()).length === 3, 'die angelegte Liste zeigt die drei Tabellen');
  // Planblätter: Listen sind (wie die anderen Listen auch) nicht platzierbar
  await befehl('planblatt_anlegen', { nummer: 'S01', titel: 'Test M4' });
  const platz = await kern('befehl', 'ansicht_platzieren', { planblatt: 'S01', ansicht: 'AN-LI-SANITAER-A-SAN' });
  pruef(platz.angenommen === false && /liste-Ansicht|Listen|liste/.test(platz.fehler ?? platz.meldung ?? ''), 'Planblatt: die Sanitär-Zusammenstellung ist – wie jede Liste – nicht platzierbar (nur CSV)', JSON.stringify(platz).slice(0, 240));

  // ================================================================== Fehler im Browser
  abschnitt('Browser-Fehler');
  const fe = await ausw(`return window.__fehler;`);
  pruef(fe.length === 0 && fehler.length === 0, 'keine JavaScript-Fehler', JSON.stringify([...fe, ...fehler]).slice(0, 400));
  const kon = konsole.filter((t) => !/Failed to load resource|favicon|net::ERR/.test(t));
  pruef(kon.length === 0, 'keine Fehlermeldungen in der Konsole', JSON.stringify(kon).slice(0, 400));
  for (const b of bilder) if (!bildPruefen(b)) pruef(false, 'Bild ist ein gültiges PNG', b);
  console.log(`\nBilder (${bilder.length}) in ${SHOTS}`);
} catch (e) {
  schlecht++;
  console.log('  FEHL  unerwarteter Fehler:', e.stack ?? e);
  try { bilder.push(await speichern('m4_fehler', null)); console.log('   Bild:', bilder.at(-1)); } catch { /* egal */ }
}

console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht ? 1 : 0);
