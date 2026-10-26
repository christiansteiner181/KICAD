// Browser-Integrationstest der Gesten (Modul G, Teil hlks-kern): Edge headless über CDP mit der Attrappe einer Kamera (--use-fake-device-for-media-stream: Testbild
// ohne Hand, --use-fake-ui-for-media-stream: Kamera erlaubt), frisches Beispielprojekt «MFH Musterweg». Nicht Teil von «node --test». Aufruf:
//   node gesten.browser.mjs        (Port 5690, CDP 9690; HLKS_GESTEN_PORT, HLKS_CDP_PORT; HLKS_BUILD wie beim Bauen)
// Der Server läuft mit HLKS_TEST=1 und allen Ablageorten unter %TEMP%\HLKS_CAD\gesten_test – nie auf W:. Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT).
//
// Prüft:
//  - navigieren() in 3D: rollen um die Blickachse (Rolle und Hochrichtung der Kamera), drehen um die Modellmitte, Treffer in der Bildmitte nach dem Rollen,
//    Haus-Knopf des Würfels und «alles zeigen» richten wieder auf; im Grundriss und im Schema verschieben und zoomen.
//  - gesten.js: starten lädt MediaPipe im Worker nur aus /lib/mediapipe/ (Netzwerk des Workers über CDP mitgeschnitten, kein fremder Server), Zustand «bereit»,
//    die Erkennung läuft (Bilder/s gemessen), testmodus, Kamera frei bei verstecktem Fenster und wieder an, Ruhebetrieb nach 10 s ohne Hand, stoppen → «aus».
// Gemessen: Erkennungen je Sekunde und Dauer je Bild im Worker (CPU, SIMD); Ausgabe am Ende.
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, rmSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_GESTEN_PORT ?? 5690);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9690);
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'gesten_test');
const hier = path.dirname(fileURLToPath(import.meta.url));

const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);
const nahe = (a, b, eps) => Math.abs(a - b) <= eps;

// ------------------------------------------------------------------ Umgebung aufbauen
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
rmSync(WURZEL, { recursive: true, force: true });
const ABLAGE = path.join(WURZEL, 'ablage');
for (const d of [ABLAGE, path.join(WURZEL, 'bib'), path.join(WURZEL, 'aus'), path.join(WURZEL, 'zuletzt'), path.join(WURZEL, 'benutzer')]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_Gesten', 'c_Pläne und Modelle', 'e_KICAD');
{
  const r = spawnSync(dotnet, [path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', projektOrdner], { encoding: 'utf8' });
  if (r.status !== 0) { console.error('Beispielprojekt nicht angelegt', r.stdout, r.stderr); process.exit(2); }
}
cpSync(path.resolve(hier, '..', '..'), path.join(WURZEL, 'wwwroot'), { recursive: true });
const env = {
  ...process.env, HLKS_WEBROOT: path.join(WURZEL, 'wwwroot'), HLKS_TEST: '1', HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: path.join(WURZEL, 'bib'),
  HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(WURZEL, 'aus'), HLKS_ZULETZT: path.join(WURZEL, 'zuletzt'), HLKS_BENUTZER: path.join(WURZEL, 'benutzer'),
  HLKS_ANLEITUNG: path.join(WURZEL, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(WURZEL, 'anleitung_pruefung'),
};
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'Gesten', '--geraet', 'gesten-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgegesten-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1280,800', '--no-first-run', '--enable-unsafe-swiftshader',
  '--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream', 'about:blank'], { stdio: 'ignore' });
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
const workerAnfragen = [];   // Netzwerk des Workers (über CDP mitgeschnitten)
ws.onmessage = (m) => {
  const d = JSON.parse(m.data);
  if (d.id && offen.has(d.id)) { offen.get(d.id)(d); offen.delete(d.id); return; }
  if (d.method === 'Runtime.exceptionThrown') fehler.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
  if (d.method === 'Runtime.consoleAPICalled' && d.params.type === 'error') konsole.push(d.params.args.map((a) => a.value ?? a.description).join(' '));
  if (d.method === 'Target.attachedToTarget') {
    const s = d.params.sessionId;
    if (d.params.targetInfo.type === 'worker') { send('Network.enable', {}, s).catch(() => {}); send('Runtime.enable', {}, s).catch(() => {}); }
    send('Runtime.runIfWaitingForDebugger', {}, s).catch(() => {});
  }
  if (d.sessionId && d.method === 'Network.requestWillBeSent') workerAnfragen.push(d.params.request.url);
  if (d.sessionId && d.method === 'Runtime.exceptionThrown') fehler.push('Worker: ' + (d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text));
};
const send = (method, params = {}, sessionId = undefined) => new Promise((res, rej) => {
  const id = ++nr;
  offen.set(id, (d) => (d.error ? rej(new Error(JSON.stringify(d.error))) : res(d.result)));
  ws.send(JSON.stringify(sessionId ? { id, method, params, sessionId } : { id, method, params }));
});
async function ausw(expr) {
  const r = await send('Runtime.evaluate', { expression: `(async()=>{${expr}})()`, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
  return r.result.value;
}
await send('Runtime.enable'); await send('Page.enable');
await send('Target.setAutoAttach', { autoAttach: true, waitForDebuggerOnStart: true, flatten: true });
await send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 800, deviceScaleFactor: 1, mobile: false });
const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };

const oeffnen = async (kz) => {
  const r = await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === ${JSON.stringify(kz)}); if (!e) return 'fehlt'; await f.ansichtOeffnen(e.id); return 'ok';`);
  if (r !== 'ok') throw new Error('Ansicht fehlt: ' + kz);
  await warte(1200);
};
const kamera = () => ausw(`const w = document.querySelector('.kachel.aktiv .d3-wurzel'); return w?.dataset.kamera ? JSON.parse(w.dataset.kamera) : null;`);
const inst = (ausdruck) => ausw(`const f = await import('/js/fenster.js'); const i = f.aktive()?.instanz; return (${ausdruck});`);
const kreuz = (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]];
const aufrecht = (k) => Math.abs(kreuz(k.blick, k.hoch)[1]) < 0.01 && k.hoch[1] > 0;   // rechts (Blick × Hoch) liegt waagrecht

const messung = {};
try {
  await send('Page.navigate', { url: SERVER + '/' });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 40000);

  // ================================================================== navigieren in 3D
  abschnitt('3D: navigieren – rollen, drehen, aufrichten');
  await oeffnen('AN-3D');
  await warteBis(async () => (await kamera()) !== null && Number((await ausw(`return document.querySelector('.kachel.aktiv .d3-wurzel')?.dataset.anzahl ?? 0;`))) > 10, '3D geladen', 30000);
  await warte(500);
  const k0 = await kamera();
  pruef(k0 && aufrecht(k0) && (await inst('i.navigierenArt')) === '3d', '3D-Ansicht aufrecht, navigierenArt «3d»', JSON.stringify(k0));
  const mitte = await ausw(`const r = document.querySelector('.kachel.aktiv .d3-leinwand').getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
  const trefferVor = await inst(`i.trefferBei(${mitte.x}, ${mitte.y})`);
  pruef(await inst(`i.navigieren({ rollen: 0.5 })`), 'navigieren({ rollen: 0.5 }) wirkt');
  await warte(200);
  const k1 = await kamera();
  pruef(nahe(k1.rolle, -0.5, 0.02), `Rolle der Kamera −0.5 (das Bild dreht gegen den Uhrzeiger): ${k1.rolle}`);
  pruef(!aufrecht(k1) && nahe(Math.hypot(...k1.blick.map((x, i) => x - k0.blick[i])), 0, 0.01), 'Blickrichtung bleibt, die Hochrichtung kippt', JSON.stringify(k1));
  pruef((await inst(`i.trefferBei(${mitte.x}, ${mitte.y})`)) === trefferVor, `Treffer in der Bildmitte nach dem Rollen gleich (${trefferVor})`);
  // eine Aktualisierung der Steuerung (Mausrad) behält die Rolle
  await send('Input.dispatchMouseEvent', { type: 'mouseWheel', x: mitte.x, y: mitte.y, deltaX: 0, deltaY: -100 });
  await warte(300);
  const k2 = await kamera();
  pruef(nahe(k2.rolle, k1.rolle, 1e-4), `Mausrad: Rolle bleibt (${k2.rolle})`);
  pruef(await inst(`i.navigieren({ drehen: [0.4, 0.2] })`), 'navigieren({ drehen }) wirkt');
  await warte(200);
  const k3 = await kamera();
  pruef(Math.hypot(...k3.blick.map((x, i) => x - k2.blick[i])) > 0.2, 'Blick hat sich gedreht', JSON.stringify(k3));
  pruef(await inst(`i.navigieren({ zoom: 1.5, verschieben: [0.05, 0] })`), 'zoom und verschieben wirken');
  // Haus-Knopf des Würfels: weich aufrichten
  await ausw(`document.querySelector('.kachel.aktiv .d3-haus').click(); return 1;`);
  await warte(800);
  const k4 = await kamera();
  pruef(k4.rolle === 0 && aufrecht(k4), `Haus-Knopf richtet auf (Rolle ${k4.rolle})`, JSON.stringify(k4));
  await inst(`i.navigieren({ rollen: -0.8 })`);
  await ausw(`(await import('/js/fenster.js')).zoomAlles(); return 1;`);
  await warte(300);
  const k5 = await kamera();
  pruef(k5.rolle === 0 && aufrecht(k5), '«Alles zeigen» richtet auf');
  pruef((await inst(`i.navigieren({})`)) === false && (await inst(`i.navigieren({ drehen: [NaN, 1] })`)) === false, 'leere bzw. ungültige Eingabe: nichts');

  abschnitt('Grundriss und Schema: verschieben und zoomen');
  for (const kz of ['AN-GR-EG', 'AN-SCHEMA-SAN']) {
    await oeffnen(kz);
    await warte(800);
    const vb = () => ausw(`const s = [...document.querySelectorAll('.kachel.aktiv .kachel-inhalt svg[viewBox]')].sort((a, b) => b.clientWidth * b.clientHeight - a.clientWidth * a.clientHeight)[0]; return s.getAttribute('viewBox').split(' ').map(Number);`);
    const a = await vb();
    pruef((await inst('i.navigierenArt')) === '2d', `${kz}: navigierenArt «2d»`);
    pruef(await inst(`i.navigieren({ verschieben: [0.1, -0.2] })`), `${kz}: verschieben wirkt`);
    const b = await vb();
    pruef(nahe(b[0], a[0] - 0.1 * a[2], 1e-6 * a[2] + 0.01) && nahe(b[1], a[1] + 0.2 * a[3], 1e-6 * a[3] + 0.01) && b[2] === a[2], `${kz}: Ausschnitt um 10 % nach links, 20 % nach unten`, JSON.stringify([a, b]));
    await inst(`i.navigieren({ zoom: 2 })`);
    const c = await vb();
    pruef(nahe(c[2], b[2] / 2, 1e-6 * b[2]) && nahe(c[0] + c[2] / 2, b[0] + b[2] / 2, 1e-6 * b[2] + 0.01), `${kz}: Zoom 2 zur Mitte`, JSON.stringify([b, c]));
    pruef((await inst(`i.navigieren({ drehen: [1, 0], rollen: 1 })`)) === false, `${kz}: drehen und rollen ohne Wirkung`);
  }

  // ================================================================== gesten.js mit der Attrappe einer Kamera
  abschnitt('gesten.js: Start, Modell aus /lib/mediapipe/, Erkennung, Bildrate');
  await oeffnen('AN-3D');
  const t0 = Date.now();
  const start = await ausw(`
    window.__gesten = [];
    const g = await import('/js/gesten/gesten.js');
    g.abonnieren((e) => window.__gesten.push(e));
    try { await g.starten({ hand: 'links' }); } catch (e) { return { fehler: e.message, name: e.name }; }
    return { laeuft: g.laeuft() };`);
  messung.startMs = Date.now() - t0;
  pruef(start?.laeuft === true, `starten: läuft (${messung.startMs} ms bis «bereit»)`, JSON.stringify(start));
  const zustaende = () => ausw(`return window.__gesten.filter((e) => e.art === 'zustand').map((e) => e.zustand);`);
  pruef((await zustaende()).includes('bereit'), 'Ereignis Zustand «bereit»', JSON.stringify(await zustaende()));
  await warte(4000);
  const d1 = await ausw(`return (await import('/js/gesten/gesten.js')).diagnose();`);
  messung.bildrate = d1.bildrate; messung.dauerMs = d1.dauerMs; messung.delegate = d1.delegate; messung.ladenMs = d1.ladenMs;
  pruef(d1.bildrate >= 12, `Erkennung läuft: ${d1.bildrate?.toFixed(1)} Bilder/s, ${d1.dauerMs?.toFixed(1)} ms je Bild im Worker (${d1.delegate}), Rückfall ${d1.rueckfall}`, JSON.stringify(d1));
  pruef(d1.kamera === true && d1.gesperrt.length === 0, 'Kamera an, keine gesperrten Anfragen', JSON.stringify(d1));
  const fremd = workerAnfragen.filter((u) => !u.startsWith(SERVER + '/lib/mediapipe/') && !u.startsWith(SERVER + '/js/gesten/'));
  pruef(workerAnfragen.some((u) => u === SERVER + '/lib/mediapipe/hand_landmarker.task') && workerAnfragen.some((u) => u.endsWith('/lib/mediapipe/wasm/vision_wasm_module_internal.wasm')), 'Modell und WASM aus /lib/mediapipe/ geladen', JSON.stringify(workerAnfragen));
  pruef(fremd.length === 0, 'der Worker fragt keinen anderen Server', JSON.stringify(fremd));
  const ohneHand = await ausw(`return window.__gesten.filter((e) => e.art === 'schritt').length;`);
  pruef(ohneHand === 0, 'Testbild ohne Hand: keine Schritt-Geste');

  abschnitt('testmodus, Fenster versteckt (Kamera frei) und zurück');
  pruef((await ausw(`const g = await import('/js/gesten/gesten.js'); return [g.testmodus(true), g.testmodus(), g.testmodus(false)];`)).join() === 'true,true,false', 'testmodus an und aus');
  await ausw(`Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true }); document.dispatchEvent(new Event('visibilitychange')); return 1;`);
  await warte(500);
  const d2 = await ausw(`return (await import('/js/gesten/gesten.js')).diagnose();`);
  pruef(d2.versteckt === true && d2.kamera === false, 'versteckt: Kamera freigegeben', JSON.stringify(d2));
  await ausw(`Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true }); document.dispatchEvent(new Event('visibilitychange')); return 1;`);
  await warte(2500);
  const d3 = await ausw(`return (await import('/js/gesten/gesten.js')).diagnose();`);
  pruef(d3.versteckt === false && d3.kamera === true && d3.bildrate > 5, `wieder sichtbar: Kamera an, Erkennung läuft (${d3.bildrate?.toFixed(1)} Bilder/s)`, JSON.stringify(d3));

  abschnitt('Ruhebetrieb: nach 10 s ohne Hand 5 Bilder/s');
  await warte(11500);
  const d4 = await ausw(`return (await import('/js/gesten/gesten.js')).diagnose();`);
  messung.ruheBildrate = d4.bildrate;
  pruef(d4.ruhe === true && nahe(d4.zielMs, 200, 1) && d4.bildrate < 7, `Ruhebetrieb: ${d4.bildrate?.toFixed(1)} Bilder/s`, JSON.stringify(d4));

  abschnitt('stoppen');
  const stopp = await ausw(`const g = await import('/js/gesten/gesten.js'); await g.stoppen(); return { laeuft: g.laeuft(), d: await g.diagnose(), letzter: window.__gesten.filter((e) => e.art === 'zustand').at(-1) };`);
  pruef(stopp.laeuft === false && stopp.letzter?.zustand === 'aus', 'stoppen: Zustand «aus»', JSON.stringify(stopp));
  messung.gesperrtBeimStopp = stopp.d.gesperrt;
  console.log('  info  beim Schliessen gesperrte Anfragen von MediaPipe:', JSON.stringify(stopp.d.gesperrt));
  pruef(fehler.length === 0, 'keine Ausnahmen', fehler.join(' | '));
  pruef(konsole.filter((k) => !/favicon/.test(k)).length === 0, 'keine Fehler in der Konsole', konsole.join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e.message);
}
console.log('\nMessung:', JSON.stringify(messung));
console.log(`\n${ok} ok, ${schlecht} fehlgeschlagen`);
await beenden(schlecht ? 1 : 0);
