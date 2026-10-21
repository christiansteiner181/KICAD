// Browser-Integrationstest der Nachbearbeitung N21 (Schnitthöhe des Grundrisses nach SIA 400): Edge headless über CDP, frisches Beispielprojekt
// «MFH Musterweg». Nicht Teil von «node --test». Aufruf:
//   node schnitthoehe.browser.mjs        (Port 5560, CDP 9560; HLKS_N21_PORT, HLKS_CDP_PORT; HLKS_BUILD wie beim Bauen)
// Der Server läuft mit HLKS_TEST=1 und allen Ablageorten unter %TEMP%\HLKS_CAD\n21_test – nie auf W:. Die Oberfläche wird aus einer Kopie ausgeliefert.
//
// Prüft in den Eigenschaften der Ansicht «Grundriss EG Sanitär»:
//  - ohne Eintrag schneidet die Ansicht nach SIA: «schnitthoehe» ist schreibgeschützt, Herkunft nennt SIA 400:2000 Ziff. C.3.1.1, der Haken ist gesetzt;
//  - Haken weg: das Feld «schnitthoehe» wird bearbeitbar, ein eingetragener Wert gilt (Abfrage schnitthoehe_sia «gilt»);
//  - Haken wieder gesetzt: die gerechnete Höhe gilt (mit einem neu eingesetzten Fenster verändert), der Handwert bleibt gespeichert.
// Bilder: %TEMP%\HLKS_CAD\n21\*.png.
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_N21_PORT ?? 5560);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9560);
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'n21');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'n21_test');
const hier = path.dirname(fileURLToPath(import.meta.url));
mkdirSync(SHOTS, { recursive: true });

const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Umgebung aufbauen
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
rmSync(WURZEL, { recursive: true, force: true });
const ABLAGE = path.join(WURZEL, 'ablage');
for (const d of [ABLAGE, path.join(WURZEL, 'bib'), path.join(WURZEL, 'aus'), path.join(WURZEL, 'zuletzt'), path.join(WURZEL, 'benutzer')]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_N21_Weg 1', 'c_Pläne und Modelle', 'e_KICAD');
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
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'N21', '--geraet', 'n21-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const kern = async (art, name, body) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const befehl = async (name, body) => { const r = await kern('befehl', name, body); if (!r.angenommen) throw new Error(`${name}: ${r.fehler ?? r.meldung}`); return r; };

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgen21-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,950', '--no-first-run', '--enable-unsafe-swiftshader', 'about:blank'], { stdio: 'ignore' });
const beenden = async (code) => {
  try { proc.kill(); } catch { /* egal */ }
  try { server.kill(); } catch { /* egal */ }
  await warte(800);
  try { rmSync(profil, { recursive: true, force: true }); } catch { /* egal */ }
  process.exit(code);
};
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); await beenden(2); }
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

const speichern = async (name) => {
  const r = await send('Page.captureScreenshot', { format: 'png' });
  const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); return f;
};
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };
const gehe = async () => {
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { if (!localStorage.getItem('hlks.design')) localStorage.setItem('hlks.design', JSON.stringify('hell')); } catch (e) {}` }).catch(() => {});
  await send('Page.navigate', { url: `${SERVER}/` });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 30000);
  await warte(700);
};
const oeffnen = async (kz) => { const r = await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === ${JSON.stringify(kz)}); if (!e) return 'fehlt'; await f.ansichtOeffnen(e.id); return 'ok';`); if (r !== 'ok') throw new Error('Ansicht fehlt: ' + kz); await warte(900); };
const auswahlSetzen = (id) => ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(${JSON.stringify(id)}); return 1;`).then(() => warte(400));
const gibt = async (sel) => (await ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`)) > 0;
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.textContent ?? null;`);
const modellWert = (kz, name) => ausw(`const z = await import('/js/zustand.js'); const a = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === ${JSON.stringify(kz)}); return a?.werte?.[${JSON.stringify(name)}] ?? null;`);
/** Haken oder Feld der Palette ändern wie ein Benutzer: Wert setzen und «change» auslösen. */
const feldSetzen = (ziel, wert, haken = false) => ausw(`const f = document.querySelector('[data-ziel=${JSON.stringify(ziel)}]'); if (!f) return false; ${haken ? `f.checked = ${wert};` : `f.value = ${JSON.stringify(String(wert))};`} f.dispatchEvent(new Event('change', { bubbles: true })); return true;`);
const AN = 'AN-GR-EG';

try {
  await gehe();
  abschnitt('N21 Palette der Ansicht: Standard = nach SIA');
  await oeffnen(AN);
  await auswahlSetzen(null);
  await warteBis(() => gibt('.ep-schnitthoehe-sia'), 'gerechnete Höhe in der Palette', 10000);
  const t1 = await text('.ep-schnitthoehe-sia');
  pruef(/1000 mm über OK Geschoss \(nach SIA 400\)/.test(t1 ?? ''), 'ohne Türen und Fenster im EG: 1000 mm als Vorgabe', t1 ?? '');
  pruef(/SIA 400:2000 Ziff\. C\.3\.1\.1/.test((await text('.ep-schnitthoehe-herkunft')) ?? ''), 'Herkunft nennt SIA 400:2000 Ziff. C.3.1.1');
  pruef(await gibt('.ep-schnitthoehe-hinweis'), 'Hinweis: keine Türen und Fenster');
  pruef(!(await gibt('input[data-ziel="eig:schnitthoehe"]')), 'Feld schnitthoehe ist schreibgeschützt (kein Eingabefeld)');
  pruef(await ausw(`return document.querySelector('input[data-ziel="eig:schnitthoeheNachSia"]')?.checked === true;`), 'Haken schnitthoeheNachSia ist gesetzt');
  await speichern('n21-palette-sia');

  abschnitt('N21 Haken weg: Handwert');
  await feldSetzen('eig:schnitthoeheNachSia', false, true);
  await warteBis(() => gibt('input[data-ziel="eig:schnitthoehe"]'), 'Feld schnitthoehe bearbeitbar', 8000);
  pruef(!(await gibt('.ep-schnitthoehe-sia')), 'gerechnete Zeile weg, Feld bearbeitbar');
  pruef((await modellWert(AN, 'schnitthoeheNachSia')) === false, 'im Modell: schnitthoeheNachSia = false');
  await feldSetzen('eig:schnitthoehe', '1500');
  await warteBis(async () => (await modellWert(AN, 'schnitthoehe')) === 1500, 'Handwert gespeichert', 8000);
  const a1 = await kern('abfrage', 'schnitthoehe_sia', { ansicht: AN });
  pruef(a1.gilt === 1500 && a1.nachSia === false, `Abfrage: gilt 1500 von Hand (gilt ${a1.gilt})`, JSON.stringify(a1).slice(0, 200));
  await speichern('n21-palette-hand');

  abschnitt('N21 Haken wieder gesetzt: gerechnete Höhe, Handwert bleibt');
  await befehl('oeffnung_setzen', { klasse: 'Fenster', wand: 'W-EG-S', abstand: 3000, breite: 1000, hoehe: 1000, bruestung: 1300 });   // 1300 … 2300
  await feldSetzen('eig:schnitthoeheNachSia', true, true);
  await warteBis(() => gibt('.ep-schnitthoehe-sia'), 'gerechnete Zeile wieder da', 8000);
  const t2 = await text('.ep-schnitthoehe-sia');
  pruef(/1300 mm über OK Geschoss/.test(t2 ?? ''), 'mit dem Fenster (Brüstung 1300) rechnet der Kern 1300 mm', t2 ?? '');
  pruef((await modellWert(AN, 'schnitthoehe')) === 1500, 'Handwert 1500 bleibt gespeichert');
  pruef(/Handwert/.test((await ausw(`return document.querySelector('.ep-inhalt')?.textContent ?? '';`)) ?? ''), 'Zeile «Handwert» zeigt ihn');
  await speichern('n21-palette-wieder-sia');

  pruef(fehler.length === 0, 'keine Skriptfehler in der Seite', fehler.join(' | ').slice(0, 400));
  const wichtig = konsole.filter((m) => !/favicon|404|ResizeObserver|WebGL|GPU stall|swiftshader/i.test(m));
  pruef(wichtig.length === 0, 'keine Fehler in der Konsole', wichtig.join(' | ').slice(0, 400));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e?.stack ?? e);
  try { await speichern('n21-abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} ok, ${schlecht} fehlgeschlagen`);
await beenden(schlecht > 0 ? 1 : 0);
