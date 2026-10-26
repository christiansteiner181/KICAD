// Browser-Integrationstest der Nachbearbeitung N3 (fehlende Bedienorte): Edge headless über CDP, echte Mausklicks und Tastatur, Beispielprojekt «MFH Musterweg» mit
// einem FIKTIVEN Zusatz (Betondecke über dem UG, Bodenplatte, zwei Rohre darin bzw. darunter, zwei Kollisionen und eine Aussparung von Hand angelegt).
// Nicht Teil von «node --test». Aufruf:  node nachbearbeitung-n3.browser.mjs        (Port 5183, CDP 9503; HLKS_N3_PORT, HLKS_CDP_PORT; HLKS_BUILD wie beim Bauen)
// Der Server läuft mit HLKS_TEST=1 und allen Ablageorten (Benutzer, zuletzt, Familienbibliothek, Projektablage, Anleitung) unter %TEMP%\HLKS_CAD\n3_test – nie auf W:.
// Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT), der Dateiwächter des Servers stürzt sonst ab, wenn im Quellordner Dateien umbenannt werden.
//
// Prüft die vier Punkte der Nachbearbeitung N3:
//  1. Einlageplan und Grundleitungsplan (Ansicht › Erstellen): Knöpfe, Dialog mit erkannten Plänen und Trockenlauf-Vorschau, Anlegen über einlageplaene_anlegen, Plan öffnet,
//     keine Doppel beim zweiten Mal, Tooltips mit «F1: Hilfe zu diesem Befehl», Menüband bei 1920 px ohne zusammengeklappte Gruppe.
//  2. Projektinformationen (Verwalten › Projekt): Dialog bearbeitbar (anlegen, ändern, ungültige Zahl, Rückgängig, Herkunft), Knoten im Projektbrowser (Doppelklick).
//  3. Kollisionsliste und Aussparungsliste im Dialog «Liste anlegen», CSV jeder Liste (Knopf «CSV …» und Kontextmenü): UTF-8 mit BOM, Semikolon, Kopfzeile, Anführungszeichen.
//  4. Tastaturkürzel (KS) nach Register: kein Kürzel eines Registers unter «Allgemein».
// Bilder (hell und dunkel): %TEMP%\HLKS_CAD\n3_*.png (SHOTS ändert den Ordner).
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, readdirSync, existsSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_N3_PORT ?? 5183);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9503);
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'n3_test');
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
for (const d of [ABLAGE, DL, path.join(WURZEL, 'bib'), path.join(WURZEL, 'aus'), path.join(WURZEL, 'zuletzt')]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_N3_Weg 1', 'c_Pläne und Modelle', 'e_KICAD');
{
  const r = spawnSync(dotnet, [path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', projektOrdner], { encoding: 'utf8' });
  if (r.status !== 0) { console.error('Beispielprojekt nicht angelegt', r.stdout, r.stderr); process.exit(2); }
}
cpSync(path.resolve(hier, '../..'), path.join(WURZEL, 'wwwroot'), { recursive: true });
const env = {
  ...process.env, HLKS_WEBROOT: path.join(WURZEL, 'wwwroot'), HLKS_TEST: '1', HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: path.join(WURZEL, 'bib'),
  HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(WURZEL, 'aus'), HLKS_ZULETZT: path.join(WURZEL, 'zuletzt'), HLKS_BENUTZER: path.join(WURZEL, 'benutzer'),
  HLKS_ANLEITUNG: path.join(WURZEL, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(WURZEL, 'anleitung_pruefung'),
};
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'N3', '--geraet', 'n3-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const kern = async (art, name, body) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const befehl = async (name, body) => { const r = await kern('befehl', name, body); if (!r.angenommen) throw new Error(`${name}: ${r.fehler ?? r.meldung}`); return r; };

// FIKTIVER Zusatz zum Beispielprojekt (alle Werte nur für diesen Test): Betondecke über dem UG, Bodenplatte, ein Rohr in der Decke (Einlage), ein Rohr unter der Bodenplatte (Grundleitung),
// zwei Kollisionen und eine Aussparung von Hand (ohne Prüflauf, damit die Listen Inhalt haben)
const modell = await (await fetch(`${SERVER}/api/modell`)).json();
const idVon = (kz) => modell.elemente.find((e) => e.kennzeichen === kz)?.id;
await befehl('element_anlegen', { klasse: 'Decke', kennzeichen: 'D-EG', geschoss: 'EG', eigenschaften: { aufbau: 'Beton 300' }, geometrie: { art: 'polygon', punkte: [[0, 0, 0], [12000, 0, 0], [12000, 10000, 0], [0, 10000, 0]], dicke: 300 } });
await befehl('element_anlegen', { klasse: 'Decke', kennzeichen: 'D-BP', geschoss: 'UG', eigenschaften: { aufbau: 'Beton 400', art: 'boden' }, geometrie: { art: 'polygon', punkte: [[0, 0, -2700], [12000, 0, -2700], [12000, 10000, -2700], [0, 10000, -2700]], dicke: 400 } });
await befehl('element_anlegen', { klasse: 'Rohr', kennzeichen: 'R-N3-1', system: 'SW', geschoss: 'UG', eigenschaften: { dn: 100, da: 110 }, geometrie: { art: 'polylinie', punkte: [[1000, 1000, -150], [5000, 1000, -150]] } });
await befehl('element_anlegen', { klasse: 'Rohr', kennzeichen: 'R-N3-2', system: 'SW', geschoss: 'UG', eigenschaften: { dn: 100, da: 110 }, geometrie: { art: 'polylinie', punkte: [[2000, 3000, -3300], [6000, 3000, -3300]] } });
await befehl('element_anlegen', { klasse: 'Kollision', kennzeichen: 'KOL-N3-1', eigenschaften: { art: 'hart', typ: 'durchdringung', status: 'neu', mass: 12.5, kennung: 'n3-1', beteiligte: [idVon('W-EG-S'), idVon('W-EG-N')] }, geometrie: { art: 'punkt', punkte: [[3000, 1000, -150]] } });
await befehl('element_anlegen', { klasse: 'Kollision', kennzeichen: 'KOL-N3-2', eigenschaften: { art: 'weich', typ: 'kontakt', status: 'bestaetigt', mass: 30, kennung: 'n3-2', kommentar: 'Rohr "verschieben"; prüfen', beteiligte: [idVon('W-EG-S'), idVon('W-EG-O')] }, geometrie: { art: 'punkt', punkte: [[2500.5, 100, 1500]] } });
await befehl('element_anlegen', { klasse: 'Aussparung', kennzeichen: 'AUS-N3-1', geschoss: 'EG', eigenschaften: { form: 'rechteckig', b: 440, h: 240, tiefe: 200, art: 'aussparung', status: 'vorgeschlagen', nummer: 'A-EG-001', kote_uk: 1880, kote_ok: 2120 }, geometrie: { art: 'punkt', punkte: [[2000, 0, 2000]] } });

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgen3-'));
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
await send('Page.setDownloadBehavior', { behavior: 'allow', downloadPath: DL }).catch(async () => { await send('Browser.setDownloadBehavior', { behavior: 'allow', downloadPath: DL }); });
let aktuelleBreite = 1920;
const breite = async (w, h = 1000) => { aktuelleBreite = w; await send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false }); await warte(350); };
await breite(1920);

const speichern = async (name, clip) => {
  const r = await send('Page.captureScreenshot', { format: 'png', ...(clip ? { clip: { ...clip, scale: 1 } } : {}) });
  const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); return f;
};
const bilder = [];
const rechteck = (sel, rand = 8) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; const r = e.getBoundingClientRect();
  const x = Math.max(0, Math.floor(r.left - ${rand})), y = Math.max(0, Math.floor(r.top - ${rand})); return { x, y, width: Math.min(innerWidth - x, Math.ceil(r.width + 2 * ${rand})), height: Math.min(innerHeight - y, Math.ceil(r.height + 2 * ${rand})) };`);
const designSetzen = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}, false); return 1;`).then(() => warte(300));
/** Bild eines Ausschnitts (Selektor; ohne: ganze Seite) in beiden Designs: n3_<name>_dunkel.png und n3_<name>_hell.png. */
async function zweiBilder(name, sel = null, rand = 8) {
  for (const d of ['dunkel', 'hell']) {
    await designSetzen(d);
    const clip = sel ? await rechteck(sel, rand) : null;
    bilder.push(await speichern(`n3_${name}_${d}`, clip));
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
const mitte = (selektor, index = 0) => ausw(`const e = document.querySelectorAll(${JSON.stringify(selektor)})[${index}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
const klickAn = async (pos, opt = {}) => {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y });
  const knopf = opt.rechts ? 'right' : 'left';
  for (const n of opt.doppel ? [1, 2] : [1]) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: knopf, buttons: opt.rechts ? 2 : 1, clickCount: n });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: knopf, buttons: 0, clickCount: n });
  }
  await warte(opt.warten ?? 250);
};
const klick = async (selektor, index = 0, opt = {}) => {
  const pos = await mitte(selektor, index);
  if (!pos) throw new Error('Element nicht gefunden: ' + selektor);
  await klickAn(pos, opt);
};
async function tippe(sel, text) {
  await klick(sel);
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.focus(); if (e.select) e.select(); return true;`);
  if (text === '') { await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 }); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 }); }
  else await send('Input.insertText', { text });
  await warte(250);
}
const wahl = (sel, wert) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) throw new Error('fehlt: ${sel}'); e.value = ${JSON.stringify(wert)}; e.dispatchEvent(new Event('change', { bubbles: true })); return e.value;`).then((v) => warte(250).then(() => v));
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const gibt = async (sel) => (await anzahl(sel)) > 0;
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const wert = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.value ?? null;`);
const registerWahl = async (name) => { await ausw(`const t = [...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(name)}); if (!t) throw new Error('Register fehlt: ${name}'); t.click(); return 1;`); await warte(250); };
const info = () => ausw(`const m = await import('/js/oberflaeche/menueband.js'); return m.bandInfo();`);
const dialoge = () => anzahl('#dialogschicht .dlg');
const dlgTitel = () => ausw(`const d = [...document.querySelectorAll('#dialogschicht .dlg')].at(-1); return d?.querySelector('.dlg-titel')?.textContent ?? null;`);
const alleDialogeZu = async () => { for (let i = 0; i < 5 && (await dialoge()) > 0; i++) await esc(); };
/** Klickt einen Knopf des obersten Dialogs nach seinem Text (echter Mausklick). */
async function dlgKnopf(beschriftung) {
  const pos = await ausw(`const d = [...document.querySelectorAll('#dialogschicht .dlg')].at(-1); const k = [...(d?.querySelectorAll('.dlg-knopf') ?? [])].find((b) => b.textContent.trim() === ${JSON.stringify(beschriftung)}); if (!k) return null; const r = k.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
  if (!pos) throw new Error('Dialogknopf fehlt: ' + beschriftung);
  await klickAn(pos, { warten: 350 });
}
const zd = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);
const aktiveAnsicht = () => ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); return z.element(f.aktive()?.id)?.kennzeichen ?? null;`);
const knopfSel = (id) => `.mb-feld .mb-knopf[data-id="${id}"]`;
const dateiWarten = async (name, ms = 10000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { const f = path.join(DL, name); if (existsSync(f) && !readdirSync(DL).some((x) => x.endsWith('.crdownload'))) return f; await warte(150); } return null; };
const bytes = (f) => readFileSync(f);

try {
  await gehe('dunkel');
  await ausw(`window.__fehler = []; window.addEventListener('error', (e) => window.__fehler.push(String(e.message))); return 1;`);

  // ================================================================== 4. Tastaturkürzel nach Register
  abschnitt('4. Tastaturkürzel (KS) nach Register');
  await registerWahl('Verwalten');
  await klick(knopfSel('verwalten.kuerzel'));
  await warteBis(async () => (await dlgTitel()) === 'Tastaturkürzel', 'KS-Dialog');
  const kz = await ausw(`return [...document.querySelectorAll('#dialogschicht .dlg tbody tr')].map((r) => [...r.children].map((c) => c.textContent));`);
  console.log(`   ${kz.length} Kürzel, Register:`, [...new Set(kz.map((z) => z[2]))].join(', '));
  const reg = (k) => kz.find((z) => z[1].split(' / ')[0] === k)?.[2];
  pruef(['TX', 'DI', 'DL', 'EL'].every((k) => reg(k) === 'Beschriften'), 'TX, DI, DL, EL stehen unter «Beschriften»', JSON.stringify(['TX', 'DI', 'DL', 'EL'].map(reg)));
  pruef(reg('CM') === 'Gebäudetechnik', 'CM (Familie platzieren) steht unter «Gebäudetechnik»', String(reg('CM')));
  pruef(reg('WA') === 'Architektur' && reg('KS') === 'Verwalten' && reg('MD') === 'Ändern' && reg('VV') === 'Ansicht', 'WA, KS, MD, VV unter ihrem Register');
  const allgemein = kz.filter((z) => z[2] === 'Allgemein').map((z) => z[1]);
  const ohneRegister = await ausw(`const a = (await import('/js/oberflaeche/aktionen.js')).aktionen; return [...a.values()].filter((x) => x.kuerzel && !x.register).map((x) => [x.kuerzel, ...x.kuerzelAlt].join(' / '));`);
  pruef(JSON.stringify(allgemein.sort()) === JSON.stringify(ohneRegister.sort()), `«Allgemein» enthält nur Aktionen ohne Register (${allgemein.join(', ') || 'keine'})`, `${allgemein} gegen ${ohneRegister}`);
  const mitRegister = await ausw(`const a = (await import('/js/oberflaeche/aktionen.js')).aktionen; return [...a.values()].filter((x) => x.kuerzel && x.register).map((x) => x.register);`);
  const bekannt = ['datei', 'architektur', 'huelle', 'gebaeudetechnik', 'einfuegen', 'beschriften', 'aendern', 'koordination', 'ansicht', 'xr', 'verwalten', 'kontext', 'familie'];
  pruef(mitRegister.every((r) => bekannt.includes(r)), 'jedes Register einer Aktion mit Kürzel hat einen Namen im Dialog', [...new Set(mitRegister)].join(','));
  await zweiBilder('kuerzel', '#dialogschicht .dlg');
  await alleDialogeZu();

  // ================================================================== Menüband: neue Knöpfe
  abschnitt('Menüband: Register Ansicht › Erstellen bei 1920 px');
  await registerWahl('Ansicht');
  const knoepfe = await ausw(`return ['ansicht.einlageplan', 'ansicht.grundleitungsplan', 'ansicht.grundriss'].map((id) => { const k = document.querySelector('.mb-feld .mb-knopf[data-id="' + id + '"]'); return k ? { id, gruppe: k.closest('.mb-gruppe').dataset.gruppe, titel: k.title, gesperrt: k.classList.contains('gesperrt'), klasse: k.className, sichtbar: k.getBoundingClientRect().width > 0 } : { id, fehlt: true }; });`);
  console.log('   ', JSON.stringify(knoepfe.map((k) => [k.id, k.gruppe, k.klasse])));
  pruef(knoepfe.every((k) => !k.fehlt && k.gruppe === 'Erstellen' && k.sichtbar), 'Einlageplan, Grundleitungsplan und Grundriss stehen in der Gruppe «Erstellen»');
  pruef(knoepfe.slice(0, 2).every((k) => !k.gesperrt), 'Einlageplan und Grundleitungsplan sind verfügbar (Projekt mit Geschossen)');
  pruef(knoepfe.slice(0, 2).every((k) => /\nF1: Hilfe zu diesem Befehl$/.test(k.titel)), 'Tooltips enden mit «F1: Hilfe zu diesem Befehl»', JSON.stringify(knoepfe.map((k) => k.titel)));
  pruef(knoepfe[0].titel.startsWith('Einlageplan') && knoepfe[1].titel.startsWith('Grundleitungsplan'), 'Tooltips nennen den Namen (die Knöpfe zeigen nur das Symbol)', JSON.stringify(knoepfe.map((k) => k.titel)));
  const REG = ['Datei', 'Architektur', 'Hülle', 'Gebäudetechnik', 'Einfügen', 'Beschriften', 'Ändern', 'Koordination', 'Ansicht', 'Verwalten'];
  for (const r of REG) {
    await registerWahl(r);
    const i = await info();
    pruef(!i.gruppen.some((g) => g.stufe === 'Z' || g.stufe === 'W') && i.weitere === 0, `${r} bei 1920 px: keine Gruppe zusammengeklappt (${i.gruppen.map((g) => g.stufe).join('')})`);
  }
  await registerWahl('Ansicht');
  bilder.push(await speichern('n3_menueband_ansicht_dunkel', { x: 0, y: 0, width: 1920, height: 150 }));
  await designSetzen('hell'); bilder.push(await speichern('n3_menueband_ansicht_hell', { x: 0, y: 0, width: 1920, height: 150 })); await designSetzen('dunkel');

  // ================================================================== 1. Einlageplan und Grundleitungsplan
  abschnitt('1. Einlageplan und Grundleitungsplan anlegen');
  const vor = await zd(`({ ansichten: z.derKlasse('Ansicht').length, blaetter: z.derKlasse('Planblatt').length, fenster: z.derKlasse('Ansichtsfenster').length })`);
  await klick(knopfSel('ansicht.einlageplan'));
  await warteBis(async () => (await dlgTitel()) === 'Einlageplan anlegen', 'Dialog Einlageplan');
  await warteBis(() => gibt('.epl-vorschau-inhalt .epl-summe'), 'Vorschau');
  const plaene = await ausw(`return [...document.querySelectorAll('.epl-tabelle tbody tr')].map((r) => [...r.children].map((c) => c.textContent));`);
  console.log('   Pläne:', JSON.stringify(plaene));
  pruef(plaene.length === 2 && plaene.some((p) => p[0] === 'Einlagen D.ü.UG' && p[1] === 'Decke') && plaene.some((p) => p[0] === 'Einlagen Bodenplatte UG' && p[1] === 'Bodenplatte'), 'der Dialog nennt die erkannten Pläne mit Planschlüssel und Bauteil (Decke, Bodenplatte)');
  pruef((await text('.epl-summe')) === 'Es entstehen: 2 Ansichten, 2 Planblätter.', 'Vorschau (Trockenlauf): 2 Ansichten, 2 Planblätter', await text('.epl-summe'));
  pruef((await text('.epl-hinweise'))?.includes('Sanitär Einlagen D.ü.UG'), 'Vorschau nennt den Titel des Blatts «Sanitär Einlagen D.ü.UG» (vom Kern)', await text('.epl-hinweise'));
  const optG = await ausw(`return [...document.querySelectorAll('.epl-geschoss option')].map((o) => o.value);`);
  pruef(optG.join() === ',UG', 'Geschoss-Auswahl: alle, UG', optG.join());
  pruef((await zd(`({ ansichten: z.derKlasse('Ansicht').length, blaetter: z.derKlasse('Planblatt').length })`)).ansichten === vor.ansichten, 'der Trockenlauf legt nichts an');
  await zweiBilder('einlageplan_dialog', '#dialogschicht .dlg');
  await dlgKnopf('Anlegen');
  await warteBis(async () => (await dialoge()) === 0 && /^EL-/.test((await aktiveAnsicht()) ?? ''), 'Plan geöffnet');
  const nach = await zd(`({ ansichten: z.derKlasse('Ansicht').length, blaetter: z.derKlasse('Planblatt').length, fenster: z.derKlasse('Ansichtsfenster').length })`);
  pruef(nach.ansichten === vor.ansichten + 2 && nach.blaetter === vor.blaetter + 2 && nach.fenster === vor.fenster + 2, 'angelegt: 2 Ansichten, 2 Planblätter, 2 Ansichtsfenster', JSON.stringify([vor, nach]));
  const el = await zd(`['EL-D.ü.UG', 'EL-BP-UG'].map((k) => { const a = z.element(k); return [k, a?.werte?.darstellung, a?.werte?.einlagen, a?.geschoss, a?.werte?.massstab]; })`);
  pruef(JSON.stringify(el) === JSON.stringify([['EL-D.ü.UG', 'einlagen', 'decke', 'UG', 50], ['EL-BP-UG', 'einlagen', 'bodenplatte', 'UG', 50]]), 'Ansichten EL-D.ü.UG und EL-BP-UG: darstellung einlagen, Bauteil, Geschoss UG, 1:50', JSON.stringify(el));
  const bl = await zd(`z.derKlasse('Planblatt').map((b) => b.werte?.titel).filter((t) => /Einlagen/.test(t))`);
  pruef(bl.includes('Sanitär Einlagen D.ü.UG') && bl.includes('Sanitär Einlagen Bodenplatte UG'), 'Planblätter «Sanitär Einlagen D.ü.UG» und «… Bodenplatte UG»', JSON.stringify(bl));
  const offene = await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); return f.alle().map((o) => z.element(o.id)?.kennzeichen);`);
  pruef(offene.some((k) => /^EL-/.test(k)), 'der neue Einlageplan ist als Ansicht geöffnet', JSON.stringify(offene));
  await warte(900);
  await zweiBilder('einlageplan_ansicht', '.kachel.aktiv', 0);
  // zweiter Aufruf: keine Doppel
  await klick(knopfSel('ansicht.einlageplan'));
  await warteBis(async () => (await dlgTitel()) === 'Einlageplan anlegen' && (await gibt('.epl-summe')), 'Dialog zum zweiten Mal');
  pruef((await text('.epl-summe')) === 'Es entstehen: nichts Neues.', 'zweiter Aufruf: «nichts Neues» (keine Doppel)', await text('.epl-summe'));
  await dlgKnopf('Abbrechen');
  // Geschoss wählen, Massstab und Format: gehen als Parameter in den Befehl
  await klick(knopfSel('ansicht.einlageplan'));
  await warteBis(async () => (await dlgTitel()) === 'Einlageplan anlegen', 'Dialog');
  await wahl('.epl-geschoss', 'UG'); await wahl('.epl-massstab', '100'); await wahl('.epl-format', 'A2');
  pruef((await anzahl('.epl-tabelle tbody tr')) === 2, 'Filter Geschoss UG: beide Pläne');
  await dlgKnopf('Abbrechen');

  await klick(knopfSel('ansicht.grundleitungsplan'));
  await warteBis(async () => (await dlgTitel()) === 'Grundleitungsplan anlegen', 'Dialog Grundleitungsplan');
  await warteBis(() => gibt('.epl-summe'), 'Vorschau');
  const gp = await ausw(`return [...document.querySelectorAll('.epl-tabelle tbody tr')].map((r) => [...r.children].map((c) => c.textContent));`);
  pruef(gp.length === 1 && gp[0][0] === 'Grundleitungen UG', 'Grundleitungsplan-Dialog nennt «Grundleitungen UG»', JSON.stringify(gp));
  pruef((await text('.epl-summe')) === 'Es entstehen: 1 Ansicht, 1 Planblatt.', 'Vorschau: 1 Ansicht, 1 Planblatt', await text('.epl-summe'));
  await zweiBilder('grundleitungsplan_dialog', '#dialogschicht .dlg');
  await dlgKnopf('Anlegen');
  await warteBis(async () => (await dialoge()) === 0 && (await aktiveAnsicht()) === 'GL-UG', 'Grundleitungsplan geöffnet');
  const gl = await zd(`[z.element('GL-UG')?.werte?.darstellung, z.element('GL-UG')?.geschoss, z.derKlasse('Planblatt').some((b) => b.werte?.titel === 'Grundleitungen UG')]`);
  pruef(JSON.stringify(gl) === JSON.stringify(['grundleitungen', 'UG', true]), 'GL-UG: darstellung grundleitungen, Geschoss UG, Planblatt «Grundleitungen UG»', JSON.stringify(gl));
  await warte(700);
  await zweiBilder('grundleitungsplan_ansicht', '.kachel.aktiv', 0);
  // Rückgängig macht den ganzen Befehl rückgängig (Kern)
  await klick('#schnellzugriff .mb-knopf[data-id="aendern.rueckgaengig"]');
  await warteBis(async () => !(await zd(`!!z.element('GL-UG')`)), 'Rückgängig entfernt GL-UG');
  pruef(!(await zd(`z.derKlasse('Planblatt').some((b) => b.werte?.titel === 'Grundleitungen UG')`)), 'Rückgängig: Ansicht und Planblatt des Grundleitungsplans sind weg (ein Befehl)');
  await klick('#schnellzugriff .mb-knopf[data-id="aendern.wiederholen"]');
  await warteBis(() => zd(`!!z.element('GL-UG')`), 'Wiederholen');
  pruef(true, 'Wiederholen legt ihn wieder an');

  // ================================================================== 3. Kollisionsliste und Aussparungsliste, CSV
  abschnitt('3. Liste anlegen: Kollisionsliste und Aussparungsliste, CSV');
  await registerWahl('Ansicht');
  await klick(knopfSel('ansicht.liste'));
  await warteBis(async () => (await dlgTitel()) === 'Liste anlegen', 'Dialog Liste anlegen');
  const optionen = await ausw(`return [...document.querySelectorAll('#dialogschicht .f-liste option')].map((o) => [o.value, o.textContent]);`);
  console.log('   Listen:', optionen.map((o) => o[1]).join(', '));
  pruef(optionen.some((o) => o[0] === 'kollisionen' && o[1] === 'Kollisionsliste') && optionen.some((o) => o[0] === 'aussparungsliste' && o[1] === 'Aussparungsliste'), 'Auswahl enthält Kollisionsliste und Aussparungsliste');
  pruef(['raumliste', 'strangliste', 'mengen', 'materialliste'].every((k) => optionen.some((o) => o[0] === k)), 'die bisherigen Listen sind noch da');
  await wahl('#dialogschicht .f-liste', 'kollisionen');
  pruef((await wert('#dialogschicht .f-name')) === 'Kollisionsliste' && (await wert('#dialogschicht .f-kz')) === 'AN-LI-KOLLISIONEN', 'Vorschlag Name «Kollisionsliste», Kennzeichen AN-LI-KOLLISIONEN');
  await zweiBilder('liste_anlegen_dialog', '#dialogschicht .dlg');
  await dlgKnopf('Anlegen');
  await warteBis(async () => (await aktiveAnsicht()) === 'AN-LI-KOLLISIONEN', 'Kollisionsliste geöffnet');
  pruef((await zd(`z.element('AN-LI-KOLLISIONEN').werte.liste`)) === 'kollisionen' && (await zd(`z.element('AN-LI-KOLLISIONEN').werte.art`)) === 'liste', 'Ansicht art liste, liste kollisionen');
  await warteBis(async () => (await anzahl('.kachel.aktiv .li-tabelle tbody tr.klickbar')) === 2, 'Kollisionsliste zeigt zwei Zeilen');
  pruef(/Kollisionsliste/.test(await text('.kachel.aktiv .li-titel')), 'Kopf: «Kollisionsliste · …»', await text('.kachel.aktiv .li-titel'));
  pruef(await gibt('.kachel.aktiv .li-kopf .li-csv'), 'Knopf «CSV …» in der Kopfzeile der Liste');
  pruef((await text('.kachel.aktiv .li-csv')) === 'CSV …', 'Beschriftung «CSV …»', await text('.kachel.aktiv .li-csv'));
  await zweiBilder('liste_kollisionen', '.kachel.aktiv', 0);

  // CSV der Kollisionsliste: Spalte «Mass mm» absteigend sortieren, dann erzeugen
  await ausw(`const th = [...document.querySelectorAll('.kachel.aktiv .li-tabelle th')].find((t) => t.textContent.startsWith('Mass')); th.click(); th.click(); return 1;`);
  await warte(250);
  await klick('.kachel.aktiv .li-csv');
  await warteBis(async () => (await dlgTitel()) === 'Liste als CSV', 'CSV-Dialog');
  const vorgabeName = await wert('#dialogschicht .csv-name');
  pruef(/^Kollisionsliste_.+_\d{4}-\d{2}-\d{2}\.csv$/.test(vorgabeName), 'Dateiname-Vorschlag: Titel_Projekt_Datum.csv', vorgabeName);
  pruef(/2 Zeilen/.test(await text('#dialogschicht .csv-dlg')) && /UTF-8 mit BOM, Semikolon/.test(await text('#dialogschicht .csv-dlg')), 'Dialog nennt Zeilenzahl und Format');
  await zweiBilder('liste_csv_dialog', '#dialogschicht .dlg');
  await tippe('#dialogschicht .csv-name', 'n3_kollisionsliste.csv');
  await dlgKnopf('Erzeugen');
  const f1 = await dateiWarten('n3_kollisionsliste.csv');
  pruef(!!f1, 'CSV-Datei ist im Download-Ordner angekommen');
  if (f1) {
    const b = bytes(f1);
    pruef(b.subarray(0, 3).toString('hex') === 'efbbbf', 'UTF-8 mit BOM (EF BB BF)', b.subarray(0, 4).toString('hex'));
    const t = b.toString('utf8').replace(/^\uFEFF/, '');
    const zeilen = t.split('\r\n');
    console.log('   ', JSON.stringify(zeilen.slice(0, 4)));
    pruef(zeilen.at(-1) === '' && zeilen.length === 4, 'Kopfzeile + zwei Datenzeilen, Zeilenende CRLF', String(zeilen.length));
    pruef(zeilen[0].startsWith('Kollision;Art;Typ;Status;Mass mm;'), 'Kopfzeile mit Spaltentiteln, Semikolon als Trenner', zeilen[0]);
    pruef(zeilen[1].startsWith('KOL-N3-2;weich;kontakt;bestätigt;30;'), 'Sortierung der Ansicht übernommen (Mass absteigend): KOL-N3-2 zuerst, Umlaut «bestätigt»', zeilen[1]);
    pruef(zeilen[2].startsWith('KOL-N3-1;hart;durchdringung;neu;12,5;'), 'Dezimalkomma wie in der Liste (12,5)', zeilen[2]);
    pruef(zeilen[1].includes('"Rohr ""verschieben""; prüfen"'), 'Feld mit Semikolon und Anführungszeichen korrekt gequotet', zeilen[1]);
    pruef(zeilen[1].includes('2500,5'), 'Lage mit Dezimalkomma (2500,5)', zeilen[1]);
    pruef(zeilen.slice(0, 3).every((z) => !z.includes('\t')), 'keine Tabulatoren');
  }

  // Aussparungsliste
  await registerWahl('Ansicht');
  await klick(knopfSel('ansicht.liste'));
  await warteBis(async () => (await dlgTitel()) === 'Liste anlegen', 'Dialog Liste anlegen');
  await wahl('#dialogschicht .f-liste', 'aussparungsliste');
  pruef((await wert('#dialogschicht .f-name')) === 'Aussparungsliste' && (await wert('#dialogschicht .f-kz')) === 'AN-LI-AUSSPARUNGEN', 'Vorschlag Name «Aussparungsliste», Kennzeichen AN-LI-AUSSPARUNGEN');
  await dlgKnopf('Anlegen');
  await warteBis(async () => (await aktiveAnsicht()) === 'AN-LI-AUSSPARUNGEN', 'Aussparungsliste geöffnet');
  await warteBis(async () => (await anzahl('.kachel.aktiv .li-tabelle tbody tr.klickbar')) === 1, 'Aussparungsliste zeigt eine Zeile');
  pruef((await zd(`z.element('AN-LI-AUSSPARUNGEN').werte.liste`)) === 'aussparungsliste', 'Ansicht liste aussparungsliste');
  await zweiBilder('liste_aussparungen', '.kachel.aktiv', 0);
  await klick('.kachel.aktiv .li-csv');
  await warteBis(async () => (await dlgTitel()) === 'Liste als CSV', 'CSV-Dialog');
  await tippe('#dialogschicht .csv-name', 'n3_aussparungsliste');   // ohne Endung: wird ergänzt
  await dlgKnopf('Erzeugen');
  const f2 = await dateiWarten('n3_aussparungsliste.csv');
  pruef(!!f2, 'CSV der Aussparungsliste angekommen (Endung .csv ergänzt)');
  if (f2) {
    const b = bytes(f2);
    const zeilen = b.toString('utf8').replace(/^\uFEFF/, '').split('\r\n');
    pruef(b.subarray(0, 3).toString('hex') === 'efbbbf', 'UTF-8 mit BOM');
    pruef(zeilen[0].split(';').slice(0, 4).join(';') === 'Nummer;Aussparung;Geschoss;Status', 'Kopfzeile Nummer;Aussparung;Geschoss;Status;…', zeilen[0]);
    pruef(zeilen[0].split(';').includes('Tiefe mm') && zeilen[0].split(';').includes('Mass mm'), 'Spalten wie in der Liste (Mass mm, Tiefe mm)', zeilen[0]);
    pruef(zeilen[1].startsWith('A-EG-001;AUS-N3-1;EG;vorgeschlagen;') && zeilen[1].includes('440 × 240'), 'Datenzeile mit Nummer, Kennzeichen, Mass 440 × 240', zeilen[1]);
    pruef(zeilen.length === 3 && zeilen[2] === '', 'Kopf + eine Zeile', String(zeilen.length));
  }

  // Kontextmenü im Projektbrowser: «CSV …»
  await tippe('.pb-suche input', 'AN-LI-KOLLISIONEN');
  await warteBis(() => gibt('.pb-zeile[data-kz="AN-LI-KOLLISIONEN"]'), 'Suchtreffer im Projektbrowser');
  await alleDialogeZu();
  await klick('.pb-zeile[data-kz="AN-LI-KOLLISIONEN"]', 0, { rechts: true });
  await warteBis(() => gibt('.kmenu'), 'Kontextmenü');
  const eintraege = await ausw(`return [...document.querySelectorAll('.kmenu button')].map((b) => b.textContent);`);
  pruef(eintraege.includes('CSV …') && eintraege.indexOf('CSV …') === eintraege.indexOf('Öffnen') + 1, 'Kontextmenü der Liste im Projektbrowser: «CSV …» nach «Öffnen»', JSON.stringify(eintraege));
  const idx = eintraege.indexOf('CSV …');
  await klick('.kmenu button', idx);
  await warteBis(async () => (await dlgTitel()) === 'Liste als CSV', 'CSV-Dialog aus dem Kontextmenü');
  await tippe('#dialogschicht .csv-name', 'n3_kontext.csv');
  await dlgKnopf('Erzeugen');
  const f3 = await dateiWarten('n3_kontext.csv');
  pruef(!!f3 && bytes(f3).subarray(0, 3).toString('hex') === 'efbbbf', 'CSV aus dem Kontextmenü erzeugt (mit BOM)');
  await tippe('.pb-suche input', '');
  // Eine Liste mit zwei Tabellen (Mengen) kommt ebenfalls an
  await registerWahl('Ansicht');
  await klick(knopfSel('ansicht.liste'));
  await warteBis(async () => (await dlgTitel()) === 'Liste anlegen', 'Dialog Liste anlegen');
  await wahl('#dialogschicht .f-liste', 'mengen');
  await dlgKnopf('Anlegen');
  await warteBis(async () => /MENGEN/.test((await aktiveAnsicht()) ?? ''), 'Mengen geöffnet');
  await warteBis(async () => (await anzahl('.kachel.aktiv .li-tabelle')) === 2, 'Mengen: zwei Tabellen');
  await klick('.kachel.aktiv .li-csv');
  await warteBis(async () => (await dlgTitel()) === 'Liste als CSV', 'CSV-Dialog');
  await tippe('#dialogschicht .csv-name', 'n3_mengen.csv');
  await dlgKnopf('Erzeugen');
  const f4 = await dateiWarten('n3_mengen.csv');
  pruef(!!f4, 'CSV jeder Listenansicht: auch «Mengen» (zwei Tabellen)');
  if (f4) {
    const zeilen = bytes(f4).toString('utf8').replace(/^\uFEFF/, '').split('\r\n');
    pruef(zeilen[0] === 'Leitungen' && zeilen.includes('Bauteile') && zeilen.includes(''), 'Mengen: Titelzeilen «Leitungen» und «Bauteile», Leerzeile dazwischen', JSON.stringify(zeilen.slice(0, 3)));
  }

  // ================================================================== 2. Projektinformationen
  abschnitt('2. Projektinformationen bearbeiten');
  pruef((await zd(`z.derKlasse('Projektinformation').length`)) === 0, 'Ausgangslage: das Beispielprojekt hat noch keine Projektinformation');
  pruef(!(await gibt('.pb-zeile[data-art="projektinfo"]')), 'ohne Element kein Knoten im Projektbrowser');
  await registerWahl('Verwalten');
  await klick(knopfSel('verwalten.projektinfo'));
  await warteBis(async () => (await dlgTitel()) === 'Projektinformationen', 'Dialog Projektinformationen');
  const felder = await ausw(`return [...document.querySelectorAll('#dialogschicht .pi [data-feld]')].map((e) => e.dataset.feld);`);
  console.log('   Felder:', felder.join(', '));
  pruef(['zeile1', 'zeile2', 'projektnummer', 'phase', 'rohrbezeichnung', 'bemassungEinheiten', 'nordwinkel'].every((f) => felder.includes(f)), 'alle bearbeitbaren Felder der Klasse sind da: Objektzeilen, Projektnummer, Phase, Rohrbezeichnung, Masseinheiten, Nordwinkel');
  const rohr = await ausw(`return [...document.querySelectorAll('#dialogschicht [data-feld="rohrbezeichnung"] option')].map((o) => o.value);`);
  pruef(rohr.join() === ',NW,DN', 'Rohrbezeichnung: NW/DN aus dem Katalog', rohr.join());
  const einheitenOpt = await ausw(`return [...document.querySelectorAll('#dialogschicht select[data-gewerk="sanitaer"] option')].map((o) => o.value);`);
  pruef(einheitenOpt.join() === ',m,cm,mm', 'Masseinheit je Gewerk: Vorgabe, m, cm, mm', einheitenOpt.join());
  const gewerke = await ausw(`return [...document.querySelectorAll('#dialogschicht select[data-gewerk]')].map((s) => s.dataset.gewerk);`);
  pruef(['sanitaer', 'heizung', 'lueftung', 'architektur'].every((g) => gewerke.includes(g)), 'Masseinheiten für jedes Gewerk', gewerke.join());
  const lesend = await text('#dialogschicht .pi');
  pruef(/Kollisionslauf/.test(lesend) && /IFC-Bezug/.test(lesend) && /HKAP-Verknüpfung/.test(lesend), 'von anderen Funktionen gesetzte Felder erscheinen nur lesend, mit Hinweis wo man sie ändert');
  await zweiBilder('projektinfo_dialog', '#dialogschicht .dlg');
  // ungültige Zahl: Dialog bleibt, Fehlertext
  await tippe('#dialogschicht [data-feld="nordwinkel"]', 'abc');
  await dlgKnopf('Übernehmen');
  pruef((await dialoge()) === 1 && /Nordwinkel: bitte eine Zahl/.test((await text('#dialogschicht .dlg-fehler')) ?? ''), 'ungültiger Nordwinkel: Dialog bleibt offen, Fehlertext', await text('#dialogschicht .dlg-fehler'));
  await zweiBilder('projektinfo_fehler', '#dialogschicht .dlg');
  // gültige Eingabe: Element wird angelegt
  await tippe('#dialogschicht [data-feld="zeile1"]', 'Testobjekt N3');
  await tippe('#dialogschicht [data-feld="zeile2"]', 'Musterweg 1, 3000 Bern (fiktiv)');
  await tippe('#dialogschicht [data-feld="projektnummer"]', 'N3-001');
  await tippe('#dialogschicht [data-feld="phase"]', 'Bauprojekt');
  await wahl('#dialogschicht [data-feld="rohrbezeichnung"]', 'DN');
  await wahl('#dialogschicht select[data-gewerk="sanitaer"]', 'mm');
  await wahl('#dialogschicht select[data-gewerk="lueftung"]', 'cm');
  await tippe('#dialogschicht [data-feld="nordwinkel"]', '12,5');
  await dlgKnopf('Übernehmen');
  await warteBis(async () => (await dialoge()) === 0, 'Dialog zu');
  await warteBis(() => zd(`z.derKlasse('Projektinformation').length === 1`), 'Projektinformation angelegt');
  const pi1 = await zd(`z.derKlasse('Projektinformation')[0].werte`);
  console.log('   Werte:', JSON.stringify(pi1));
  pruef(pi1.zeile1 === 'Testobjekt N3' && pi1.zeile2 === 'Musterweg 1, 3000 Bern (fiktiv)' && pi1.projektnummer === 'N3-001' && pi1.phase === 'Bauprojekt' && pi1.rohrbezeichnung === 'DN' && pi1.nordwinkel === 12.5, 'Projektinformation angelegt: Objektzeilen, Nummer, Phase, DN, Nordwinkel 12,5', JSON.stringify(pi1));
  pruef(JSON.stringify(pi1.bemassungEinheiten) === JSON.stringify({ sanitaer: 'mm', lueftung: 'cm' }), 'Masseinheiten je Gewerk: Sanitär mm, Lüftung cm', JSON.stringify(pi1.bemassungEinheiten));
  const piKz = await zd(`z.derKlasse('Projektinformation')[0].kennzeichen`);
  console.log('   Kennzeichen:', piKz);
  // Wirkung im Kern: Rohrbezeichnung DN → Beschriftung «DN»? (der Kern entscheidet; hier nur: die Abfrage bemassungseinheit/Beschriftung kennt den Wert nicht ohne Ansicht) – Herkunft prüfen
  const warum = await kern('abfrage', 'warum', { element: piKz, eigenschaft: 'rohrbezeichnung' });
  console.log('   Herkunft:', JSON.stringify(warum).slice(0, 300));
  pruef(JSON.stringify(warum).includes('N3'), 'Herkunft (warum): der Akteur der Änderung ist festgehalten', JSON.stringify(warum).slice(0, 200));
  // Knoten im Projektbrowser
  await registerWahl('Ansicht');
  await warteBis(() => gibt('.pb-zeile[data-art="projektinfo"]'), 'Knoten Projektinformationen');
  pruef((await text('.pb-zeile[data-art="projektinfo"]')).includes('Projektinformationen'), 'Projektbrowser: Gebäude › «Projektinformationen»', await text('.pb-zeile[data-art="projektinfo"]'));
  // Ändern: zweite Änderung nur einer Zeile, danach Rückgängig stellt den vorigen Stand her
  await klick('.pb-zeile[data-art="projektinfo"]', 0, { doppel: true });
  await warteBis(async () => (await dlgTitel()) === 'Projektinformationen', 'Doppelklick auf den Knoten öffnet den Dialog');
  pruef((await wert('#dialogschicht [data-feld="zeile1"]')) === 'Testobjekt N3' && (await wert('#dialogschicht [data-feld="nordwinkel"]')) === '12,5' && (await wert('#dialogschicht [data-feld="rohrbezeichnung"]')) === 'DN', 'der Dialog zeigt die gespeicherten Werte (Nordwinkel mit Komma)');
  pruef((await wert('#dialogschicht select[data-gewerk="sanitaer"]')) === 'mm' && (await wert('#dialogschicht select[data-gewerk="heizung"]')) === '', 'Masseinheiten gespeichert: Sanitär mm, Heizung Vorgabe');
  await tippe('#dialogschicht [data-feld="phase"]', 'Ausführung');
  await wahl('#dialogschicht [data-feld="rohrbezeichnung"]', 'NW');
  await dlgKnopf('Übernehmen');
  await warteBis(() => zd(`z.derKlasse('Projektinformation')[0].werte.phase === 'Ausführung'`), 'Phase geändert');
  const pi2 = await zd(`z.derKlasse('Projektinformation')[0].werte`);
  pruef(pi2.phase === 'Ausführung' && pi2.rohrbezeichnung === 'NW' && pi2.zeile1 === 'Testobjekt N3' && pi2.projektnummer === 'N3-001', 'nur die geänderten Felder ändern sich', JSON.stringify(pi2));
  pruef(JSON.stringify(pi2.bemassungEinheiten) === JSON.stringify(pi1.bemassungEinheiten), 'Masseinheiten bleiben');
  // Ohne Änderung: Übernehmen sendet nichts
  const pakete = (await (await fetch(`${SERVER}/api/status`)).json()).stand?.pakete ?? null;
  await registerWahl('Verwalten'); await klick(knopfSel('verwalten.projektinfo'));
  await warteBis(async () => (await dlgTitel()) === 'Projektinformationen', 'Dialog');
  await dlgKnopf('Übernehmen');
  await warteBis(async () => (await dialoge()) === 0, 'Dialog zu ohne Befehl');
  await warte(500);
  const paketeNach = (await (await fetch(`${SERVER}/api/status`)).json()).stand?.pakete ?? null;
  pruef(paketeNach === pakete, 'ohne Änderung wird kein Befehl gesendet', `${pakete} → ${paketeNach}`);
  // Rückgängig
  await klick('#schnellzugriff .mb-knopf[data-id="aendern.rueckgaengig"]');
  await warteBis(() => zd(`z.derKlasse('Projektinformation')[0].werte.phase === 'Bauprojekt'`), 'Rückgängig stellt die Phase wieder her');
  const pi3 = await zd(`z.derKlasse('Projektinformation')[0].werte`);
  pruef(pi3.phase === 'Bauprojekt' && pi3.rohrbezeichnung === 'DN', 'Rückgängig: Phase «Bauprojekt» und DN wie vor der Änderung (ein Befehl)', JSON.stringify(pi3));
  await klick('#schnellzugriff .mb-knopf[data-id="aendern.rueckgaengig"]');
  await warteBis(async () => (await zd(`z.derKlasse('Projektinformation').length`)) === 0, 'zweites Rückgängig entfernt das angelegte Element');
  pruef(true, 'zweites Rückgängig: das beim ersten Übernehmen angelegte Element verschwindet wieder');
  await warteBis(async () => !(await gibt('.pb-zeile[data-art="projektinfo"]')), 'Knoten weg');
  await klick('#schnellzugriff .mb-knopf[data-id="aendern.wiederholen"]');
  await warteBis(async () => (await zd(`z.derKlasse('Projektinformation').length`)) === 1, 'Wiederholen');
  await registerWahl('Verwalten'); await klick(knopfSel('verwalten.projektinfo'));
  await warteBis(async () => (await dlgTitel()) === 'Projektinformationen', 'Dialog');
  await zweiBilder('projektinfo_gefuellt', '#dialogschicht .dlg');
  // Überblick (wie bisher) ausklappen
  await ausw(`document.querySelector('#dialogschicht .pi-ueberblick summary').scrollIntoView({ block: 'end' }); return 1;`);
  await klick('#dialogschicht .pi-ueberblick summary');
  await warteBis(async () => /Prüfsumme/.test((await text('#dialogschicht .pi-ueberblick-inhalt')) ?? ''), 'Überblick geladen');
  pruef(/MFH Musterweg/.test(await text('#dialogschicht .pi-ueberblick-inhalt')), 'der bisherige Überblick über das Projekt steht ausklappbar im Dialog');
  await alleDialogeZu();

  // ================================================================== Seitenfehler
  abschnitt('Keine Fehler in der Seite');
  pruef(bilder.length > 0 && bilder.every(bildPruefen), `${bilder.length} Bilder gespeichert (PNG)`, bilder.filter((b) => !bildPruefen(b)).join(', '));
  console.log('   Bilder in', SHOTS, '(n3_*.png)');
  const seitenfehler = (await ausw('return window.__fehler;')) ?? [];
  pruef(fehler.length === 0 && seitenfehler.length === 0, 'keine Ausnahmen in der Seite', JSON.stringify([...fehler, ...seitenfehler]).slice(0, 600));
  const echteKonsole = konsole.filter((k) => !/Failed to load resource|favicon|net::ERR/.test(k));
  pruef(echteKonsole.length === 0, 'keine Konsolenfehler', echteKonsole.join(' | ').slice(0, 600));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e.stack ?? e);
  try { bilder.push(await speichern('n3_abbruch')); console.log('   Bild:', bilder.at(-1)); } catch { /* egal */ }
} finally {
  console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
  try { ws.close(); } catch { /* egal */ }
  proc.kill(); server.kill();
  process.exit(schlecht > 0 ? 1 : 0);
}
