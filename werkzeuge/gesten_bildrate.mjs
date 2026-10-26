// Bildratenmessung der 3D-Ansicht mit und ohne Gesten (Modul G, «Fertig, wenn»: höchstens 10 % unter der Bildrate ohne Gesten; bei ausgeschalteten Gesten Kamera aus,
// keine Last). Edge über CDP mit der Attrappe einer Kamera (--use-fake-device-for-media-stream), frisches Beispielprojekt «MFH Musterweg», Server dieses Stands.
// Aufruf:  node werkzeuge/gesten_bildrate.mjs        (Port 5172, CDP 9172; HLKS_BR_PORT, HLKS_BR_CDP; HLKS_BUILD wie beim Bauen; HLKS_BR_SEKUNDEN = Dauer je Zustand, Vorgabe 20;
//                                                     HLKS_BR_SICHTBAR=1 = Edge mit Fenster statt headless; HLKS_BR_UNGEKLEMMT=1 = ohne 60-Hz-Grenze, --disable-frame-rate-limit --disable-gpu-vsync)
// Ablauf: 3D-Ansicht öffnen, die Kamera dreht sich gleichmässig (je Bild ein kleiner Schritt über navigieren({ drehen }), also gleiche Arbeit je Bild), mit
// requestAnimationFrame über SEKUNDEN s die Bildzeiten zählen. Drei Zustände nacheinander: (a) Gesten aus, (b) Gesten an (über die Steuerung wie der Schalter der
// Statusleiste), (c) wieder aus. Ausgabe: Bilder/s (Anzahl / Dauer), Mittel der Bildzeit, 5-%-Quantil der Bildrate (Bildzeit am 95-%-Perzentil), Differenz in %.
// Prüft zusätzlich über Fühler, die vor jeder Seite eingesetzt werden (getUserMedia und Worker umhüllt): in (a) und (c) keine offene Kamera (kein Track «live», kein
// Anruf von getUserMedia vor dem ersten Einschalten) und kein lebender Worker «KICAD-Gesten».
// Server mit HLKS_TEST=1, alle Ablageorte unter %TEMP%\HLKS_CAD\gesten_bildrate – nie auf W:. Beendet nur die eigenen Prozesse (eigene PID).
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, rmSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_BR_PORT ?? 5172);
const CDPPORT = Number(process.env.HLKS_BR_CDP ?? 9172);
const SEKUNDEN = Number(process.env.HLKS_BR_SEKUNDEN ?? 20);
const SICHTBAR = process.env.HLKS_BR_SICHTBAR === '1';
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const SERVER = `http://localhost:${PORT}`;
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'gesten_bildrate');
const hier = path.dirname(fileURLToPath(import.meta.url));
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };

if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
rmSync(WURZEL, { recursive: true, force: true });
const ABLAGE = path.join(WURZEL, 'ablage');
for (const d of [ABLAGE, path.join(WURZEL, 'bib'), path.join(WURZEL, 'aus'), path.join(WURZEL, 'zuletzt'), path.join(WURZEL, 'benutzer')]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_Bildrate', 'c_Pläne und Modelle', 'e_KICAD');
{
  const r = spawnSync(dotnet, [path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', projektOrdner], { encoding: 'utf8' });
  if (r.status !== 0) { console.error('Beispielprojekt nicht angelegt', r.stdout, r.stderr); process.exit(2); }
}
cpSync(path.resolve(hier, '..', 'src', 'Hlks.Web', 'wwwroot'), path.join(WURZEL, 'wwwroot'), { recursive: true });
const env = {
  ...process.env, HLKS_WEBROOT: path.join(WURZEL, 'wwwroot'), HLKS_TEST: '1', HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: path.join(WURZEL, 'bib'),
  HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(WURZEL, 'aus'), HLKS_ZULETZT: path.join(WURZEL, 'zuletzt'), HLKS_BENUTZER: path.join(WURZEL, 'benutzer'),
  HLKS_ANLEITUNG: path.join(WURZEL, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(WURZEL, 'anleitung_pruefung'),
};
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'Bildrate', '--geraet', 'bildrate-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }

const profil = mkdtempSync(path.join(tmpdir(), 'edgebildrate-'));
const edgeArgs = [`--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1280,800', '--no-first-run', '--enable-unsafe-swiftshader',
  '--disable-renderer-backgrounding', '--disable-background-timer-throttling', '--disable-backgrounding-occluded-windows',
  '--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream', 'about:blank'];
if (process.env.HLKS_BR_UNGEKLEMMT === '1') edgeArgs.unshift('--disable-frame-rate-limit', '--disable-gpu-vsync');
if (!SICHTBAR) edgeArgs.unshift('--headless=new');
const proc = spawn(edge, edgeArgs, { stdio: 'ignore' });
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
let nr = 0; const offen = new Map(); const fehler = [];
ws.onmessage = (m) => {
  const d = JSON.parse(m.data);
  if (d.id && offen.has(d.id)) { offen.get(d.id)(d); offen.delete(d.id); return; }
  if (d.method === 'Runtime.exceptionThrown') fehler.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
};
const send = (method, params = {}) => new Promise((res, rej) => { const id = ++nr; offen.set(id, (d) => (d.error ? rej(new Error(JSON.stringify(d.error))) : res(d.result))); ws.send(JSON.stringify({ id, method, params })); });
async function ausw(expr) {
  const r = await send('Runtime.evaluate', { expression: `(async()=>{${expr}})()`, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
  return r.result.value;
}
await send('Runtime.enable'); await send('Page.enable');
await send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 800, deviceScaleFactor: 1, mobile: false });
// Fühler vor jeder Seite: getUserMedia (Anrufe, Streams) und Worker «KICAD-Gesten» (gebaut, beendet)
await send('Page.addScriptToEvaluateOnNewDocument', { source: `
  window.__fuehler = { gum: 0, streams: [], worker: 0, workerEnde: 0 };
  const md = navigator.mediaDevices;
  if (md?.getUserMedia) { const o = md.getUserMedia.bind(md); md.getUserMedia = async (...a) => { window.__fuehler.gum++; const s = await o(...a); window.__fuehler.streams.push(s); return s; }; }
  const W = window.Worker;
  window.Worker = class extends W {
    constructor(u, o) { super(u, o); this.__g = o?.name === 'KICAD-Gesten'; if (this.__g) window.__fuehler.worker++; }
    terminate() { if (this.__g && !this.__beendet) { this.__beendet = true; window.__fuehler.workerEnde++; } super.terminate(); }
  };` });
const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };
const fuehler = () => ausw(`const f = window.__fuehler; return { gum: f.gum, kameraLive: f.streams.flatMap((s) => s.getTracks()).filter((t) => t.readyState === 'live').length, workerLebt: f.worker - f.workerEnde };`);

// Messung im Seitenkontext: je Bild ein Drehschritt, rAF-Zeiten sammeln
const messen = (sek) => ausw(`
  const f = await import('/js/fenster.js'); const i = f.aktive().instanz;
  const zeiten = []; let letzte = performance.now(); const ende = letzte + ${sek} * 1000;
  await new Promise((fertig) => { const schritt = (t) => { zeiten.push(t - letzte); letzte = t; i.navigieren({ drehen: [0.01, 0] }); if (t < ende) requestAnimationFrame(schritt); else fertig(); }; requestAnimationFrame(schritt); });
  zeiten.shift();
  const sort = [...zeiten].sort((a, b) => a - b);
  const summe = zeiten.reduce((s, x) => s + x, 0);
  return { bilder: zeiten.length, dauerS: summe / 1000, bildrate: zeiten.length / (summe / 1000), mittelMs: summe / zeiten.length, p95Ms: sort[Math.floor(sort.length * 0.95)], maxMs: sort.at(-1) };`);
const fmt = (m) => `${m.bildrate.toFixed(2)} Bilder/s (${m.bilder} Bilder in ${m.dauerS.toFixed(1)} s), Bildzeit Mittel ${m.mittelMs.toFixed(1)} ms, 95 %: ${m.p95Ms.toFixed(1)} ms (= 5-%-Quantil der Bildrate ${(1000 / m.p95Ms).toFixed(1)}/s), max ${m.maxMs.toFixed(0)} ms`;

const erg = {};
try {
  await send('Page.navigate', { url: SERVER + '/' });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 40000);
  const r = await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-3D'); await f.ansichtOeffnen(e.id); return 'ok';`);
  if (r !== 'ok') throw new Error('3D-Ansicht fehlt');
  await warteBis(async () => Number(await ausw(`return document.querySelector('.kachel.aktiv .d3-wurzel')?.dataset.anzahl ?? 0;`)) > 10, '3D geladen', 30000);
  await warte(1500);
  console.log(`Messart: Edge ${SICHTBAR ? 'mit Fenster' : 'headless (new)'}, 1280 × 800, Software-Rendering (SwiftShader), rAF${process.env.HLKS_BR_UNGEKLEMMT === '1' ? ' ohne 60-Hz-Grenze' : ''}, ${SEKUNDEN} s je Zustand, Drehschritt je Bild`);
  await ausw(`window.__gesten = []; return 1;`);

  console.log('\n== (a) Gesten aus');
  const fa = await fuehler();
  pruef(fa.gum === 0 && fa.kameraLive === 0 && fa.workerLebt === 0, 'vor dem Einschalten: getUserMedia nie aufgerufen, keine offene Kamera, kein Worker', JSON.stringify(fa));
  erg.anlauf = await messen(SEKUNDEN); console.log('   Anlauf (nicht gewertet, die erste Messung nach dem Start liegt in dieser Umgebung höher):', fmt(erg.anlauf));
  erg.a = await messen(SEKUNDEN); console.log('  ', fmt(erg.a));
  const fa2 = await fuehler();
  pruef(fa2.gum === 0 && fa2.kameraLive === 0 && fa2.workerLebt === 0, 'nach der Messung (a): weiterhin keine Kamera, kein Worker', JSON.stringify(fa2));

  if (process.env.HLKS_BR_KONTROLLE === '1') {   // Kontrolle ohne Gesten (HLKS_BR_KONTROLLE=1): zweimal weiter «aus», zeigt die Drift der Messung selbst
    for (const n of ['a2', 'a3']) { await messen(3); erg[n] = await messen(SEKUNDEN); console.log(`   ${n}:`, fmt(erg[n])); }
    console.log('\nMessung:', JSON.stringify(erg)); await beenden(0);
  }
  console.log('\n== (b) Gesten an (Testkamera)');
  const an = await ausw(`const s = await import('/js/gesten/gesten-steuerung.js'); const g = await import('/js/gesten/gesten.js'); const ok = await s.steuerung.schalten(true); return { ok, laeuft: g.laeuft(), d: await g.diagnose() };`);
  pruef(an.ok === true && an.laeuft === true, 'Gesten eingeschaltet', JSON.stringify(an));
  await warte(3000);
  const fb = await fuehler();
  pruef(fb.gum >= 1 && fb.kameraLive >= 1 && fb.workerLebt === 1, 'Kamera offen, Worker läuft', JSON.stringify(fb));
  const dB = await ausw(`return (await import('/js/gesten/gesten.js')).diagnose();`);
  console.log(`   Erkennung im Worker: ${dB.bildrate?.toFixed(1)} Bilder/s, ${dB.dauerMs?.toFixed(1)} ms je Bild, ${dB.delegate}, Ruhe ${dB.ruhe}`);
  erg.b = await messen(SEKUNDEN); console.log('  ', fmt(erg.b));
  erg.dB = await ausw(`return (await import('/js/gesten/gesten.js')).diagnose();`);
  console.log(`   Erkennung im Worker nach der Messung: ${erg.dB.bildrate?.toFixed(1)} Bilder/s, ${erg.dB.dauerMs?.toFixed(1)} ms je Bild, Ruhe ${erg.dB.ruhe}`);

  console.log('\n== (c) Gesten wieder aus');
  await ausw(`const s = await import('/js/gesten/gesten-steuerung.js'); await s.steuerung.schalten(false); return 1;`);
  await warte(1500);
  const fc = await fuehler();
  pruef(fc.kameraLive === 0 && fc.workerLebt === 0, 'nach dem Ausschalten: kein Track «live», Worker beendet', JSON.stringify(fc));
  await messen(3);
  erg.c = await messen(SEKUNDEN); console.log('  ', fmt(erg.c));
  const fc2 = await fuehler();
  pruef(fc2.gum === fb.gum && fc2.kameraLive === 0 && fc2.workerLebt === 0, 'während (c): keine neue Kamera, kein Worker', JSON.stringify(fc2));

  const diff = (x, y) => ((x - y) / y) * 100;
  erg.diffBA = diff(erg.b.bildrate, erg.a.bildrate); erg.diffBC = diff(erg.b.bildrate, erg.c.bildrate); erg.diffCA = diff(erg.c.bildrate, erg.a.bildrate);
  erg.diffBMittel = Math.max(diff(erg.b.bildrate, erg.a.bildrate), diff(erg.b.bildrate, erg.c.bildrate));
  console.log(`\nDifferenz (b gegen a): ${erg.diffBA.toFixed(1)} %   (b gegen c): ${erg.diffBC.toFixed(1)} %   (c gegen a, Messrauschen): ${erg.diffCA.toFixed(1)} %`);
  const ohne = (erg.a.bildrate + erg.c.bildrate) / 2;
  erg.diffMitOhne = diff(erg.b.bildrate, ohne);
  console.log(`Differenz (b gegen Mittel von a und c = ${ohne.toFixed(2)} Bilder/s): ${erg.diffMitOhne.toFixed(1)} %`);
  pruef(erg.diffMitOhne >= -10, 'Bildrate mit Gesten höchstens 10 % unter der ohne Gesten');
  pruef(fehler.length === 0, 'keine Ausnahmen', fehler.join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e.message);
}
console.log('\nMessung:', JSON.stringify(erg));
console.log(`\n${ok} ok, ${schlecht} fehlgeschlagen`);
await beenden(schlecht ? 1 : 0);
