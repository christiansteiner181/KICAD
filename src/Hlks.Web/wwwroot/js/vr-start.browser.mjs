// Browser-Integrationstest der VR-Startseite /vr (Modul XR): Edge headless über CDP, echte Mausklicks, frisches Beispielprojekt «MFH Musterweg» – OHNE Brille:
// navigator.xr ist eine eingespritzte Attrappe (wie in vr.browser.mjs); Kopf, Sticks und die Anbindung an three kommen über die Test-Nähte von vr3d.js.
// Nicht Teil von «node --test». Aufruf:
//   node vr-start.browser.mjs        (Port 5680, CDP 9680; HLKS_VRSTART_PORT, HLKS_CDP_PORT; HLKS_BUILD wie beim Bauen)
// Der Server läuft mit HLKS_TEST=1 und allen Ablageorten unter %TEMP%\HLKS_CAD\vrstart_test – nie auf W:. Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT).
//
// Prüft:
//  - /vr lädt ohne Menüband und Projektbrowser; der Knopf «Eintreten» füllt den Bildschirm, zeigt Projekt, Geschoss und Raum des Startpunkts (erster Raum im Erdgeschoss).
//  - Ein Tipp irgendwo startet die Sitzung genau einmal (immersive-vr, local-floor), am Standpunkt des Raums.
//  - Ende der Sitzung (Quest-Menü) → zurück auf der Startseite mit «Eintreten», nicht in der PC-Oberfläche; der Standpunkt ist gemerkt (Benutzereinstellung vr.letzter_standpunkt).
//  - Neues Laden nimmt den gemerkten Standpunkt; ein gültiges ?raum=<Id> hat Vorrang; ein ungültiges wird ignoriert.
//  - Manifest erreichbar und gültig, Symbole 192 und 512 px; keine Konsolenfehler.
// Bilder: %TEMP%\HLKS_CAD\vrstart\*.png (SHOTS ändert den Ordner).
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_VRSTART_PORT ?? 5680);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9680);
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'vrstart');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'vrstart_test');
const hier = path.dirname(fileURLToPath(import.meta.url));
mkdirSync(SHOTS, { recursive: true });

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
const projektOrdner = path.join(ABLAGE, 'Test_VRStart', 'c_Pläne und Modelle', 'e_KICAD');
{
  const r = spawnSync(dotnet, [path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', projektOrdner], { encoding: 'utf8' });
  if (r.status !== 0) { console.error('Beispielprojekt nicht angelegt', r.stdout, r.stderr); process.exit(2); }
}
cpSync(path.resolve(hier, '..'), path.join(WURZEL, 'wwwroot'), { recursive: true });
const env = {
  ...process.env, HLKS_WEBROOT: path.join(WURZEL, 'wwwroot'), HLKS_TEST: '1', HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: path.join(WURZEL, 'bib'),
  HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(WURZEL, 'aus'), HLKS_ZULETZT: path.join(WURZEL, 'zuletzt'), HLKS_BENUTZER: path.join(WURZEL, 'benutzer'),
  HLKS_ANLEITUNG: path.join(WURZEL, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(WURZEL, 'anleitung_pruefung'),
};
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'VRStart', '--geraet', 'vrstart-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const holenJson = async (pfad) => (await fetch(`${SERVER}${pfad}`)).json();

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgevrs-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1280,800', '--no-first-run', '--enable-unsafe-swiftshader', 'about:blank'], { stdio: 'ignore' });
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
await send('Emulation.setDeviceMetricsOverride', { width: 1280, height: 800, deviceScaleFactor: 1, mobile: false });

const speichern = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); return f; };
const bildPruefen = (pfad) => { const b = readFileSync(pfad); return b.subarray(0, 8).toString('hex') === '89504e470d0a1a0a' && b.length > 1500; };
const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };

// Attrappe für navigator.xr: wie in vr.browser.mjs; zuerst ohne Brille (_unterstuetzt false)
const ATTRAPPE = `(() => {
  const ziel = new EventTarget();
  const xr = {
    _unterstuetzt: false, anfragen: [], sitzung: null, sitzungen: 0,
    isSessionSupported: async (art) => art === 'immersive-vr' && xr._unterstuetzt,
    requestSession: async (art, opt) => {
      xr.anfragen.push({ art, opt: JSON.parse(JSON.stringify(opt ?? {})) });
      if (!xr._unterstuetzt) throw new DOMException('Keine VR-Brille', 'NotSupportedError');
      const s = new EventTarget(); s.ended = false; xr.sitzung = s; xr.sitzungen++;
      s.end = async () => { if (s.ended) return; s.ended = true; s.dispatchEvent(new Event('end')); };
      return s;
    },
    addEventListener: (...a) => ziel.addEventListener(...a), removeEventListener: (...a) => ziel.removeEventListener(...a), dispatchEvent: (e) => ziel.dispatchEvent(e),
  };
  Object.defineProperty(navigator, 'xr', { value: xr, configurable: true });
  window.__xr = xr;
  window.__kopf = { x: 0, y: 1.62, z: 0, gieren: Math.PI / 2 };
  window.__eingabe = { links: { x: 0, y: 0, schnell: false }, rechts: { x: 0 }, ifc: false };
})();`;
await send('Page.addScriptToEvaluateOnNewDocument', { source: ATTRAPPE });

const KNOPF = '#vr-eintreten';
const gehe = async (pfad = '/vr') => {
  await send('Page.navigate', { url: `${SERVER}${pfad}` });
  await warteBis(() => ausw(`return document.readyState === 'complete' && ['bereit', 'kein-projekt', 'fehler'].includes(document.querySelector('${KNOPF}')?.dataset.status);`), 'Startseite bereit', 40000);
  await warte(300);
};
const naht = (ausdruck) => ausw(`const m = await import('/js/vr-start.js'); const n = m.naht; return (${ausdruck});`);
const knopfInfo = () => ausw(`const k = document.querySelector('${KNOPF}'); const r = k.getBoundingClientRect();
  return { status: k.dataset.status, quelle: k.dataset.startquelle, text: k.innerText.replace(/\\s+/g, ' ').trim(), projekt: document.getElementById('vr-projekt').textContent,
    geschoss: document.getElementById('vr-geschoss').textContent, raum: document.getElementById('vr-raum').textContent, hinweis: document.getElementById('vr-hinweis').textContent,
    x: r.left, y: r.top, b: r.width, h: r.height, anzeige: getComputedStyle(k).display, fenster: { b: innerWidth, h: innerHeight } };`);
const anfragen = () => ausw(`return window.__xr.anfragen;`);
const klickAn = async (x, y) => {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, buttons: 0 });
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: 'left', buttons: 1, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: 'left', buttons: 0, clickCount: 1 });
};
const einstellung = async () => (await holenJson('/api/benutzer/einstellungen')).werte?.['vr.letzter_standpunkt'] ?? null;
const natuerlich = (a, b) => String(a).localeCompare(String(b), 'de', { numeric: true });

// ------------------------------------------------------------------ Ablauf
try {
  const modell = await holenJson('/api/modell');
  const gs = modell.elemente.filter((e) => e.klasse === 'Geschoss').map((g) => ({ kz: g.kennzeichen, name: g.name, kote: Number(g.werte.kote) })).sort((a, b) => a.kote - b.kote);
  const eg = gs.find((g) => g.kote >= 0);
  const raeume = modell.elemente.filter((e) => e.klasse === 'Raum');
  const raeumeEG = raeume.filter((r) => r.geschoss === eg.kz).sort((a, b) => natuerlich(a.kennzeichen, b.kennzeichen));
  const raumOG = raeume.find((r) => r.geschoss !== eg.kz && gs.find((g) => g.kz === r.geschoss)?.kote > 0);
  pruef(eg && raeumeEG.length >= 2 && raumOG, `Beispielprojekt: Erdgeschoss ${eg?.kz} mit ${raeumeEG.length} Räumen, Raum im Obergeschoss ${raumOG?.kennzeichen}`);

  // ================================================================== Startseite
  abschnitt('/vr ohne Menüband: Knopf «Eintreten» mit Projekt, Geschoss und Raum');
  await gehe();
  const oberflaeche = await ausw(`return { band: !!document.getElementById('menueband'), baum: !!document.querySelector('.mb-tab, #seitenleiste, #dock-rechts, #kopf, .projektbrowser'), titel: document.title };`);
  pruef(!oberflaeche.band && !oberflaeche.baum, 'kein Menüband, kein Projektbrowser, keine PC-Oberfläche', JSON.stringify(oberflaeche));
  const k0 = await knopfInfo();
  pruef(k0.status === 'bereit' && /Eintreten/.test(k0.text), `Knopf «Eintreten» steht bereit: «${k0.text}»`, JSON.stringify(k0));
  pruef(k0.anzeige !== 'none' && nahe(k0.b, k0.fenster.b, 1) && nahe(k0.h, k0.fenster.h, 1) && nahe(k0.x, 0, 1) && nahe(k0.y, 0, 1), `der Knopf füllt den Bildschirm (${k0.b} × ${k0.h})`, JSON.stringify(k0));
  pruef(k0.projekt.includes('MFH Musterweg'), `Projektname: «${k0.projekt}»`);
  const raum1 = raeumeEG[0];
  pruef(k0.quelle === 'eg' && k0.geschoss.includes(eg.name ?? eg.kz), `Geschoss des Startpunkts: «${k0.geschoss}», Quelle ${k0.quelle}`, JSON.stringify(k0));
  pruef(k0.raum.includes(raum1.name ?? raum1.kennzeichen) || k0.raum.includes(raum1.kennzeichen), `Raum des Startpunkts: «${k0.raum}» (erster Raum im EG: ${raum1.kennzeichen} ${raum1.name ?? ''})`, JSON.stringify(k0));
  pruef(/Keine VR-Brille erkannt/.test(k0.hinweis), `ohne Brille steht ein Hinweis: «${k0.hinweis}»`);
  pruef((await naht('n.standort()?.raum')) === raum1.kennzeichen, 'der Standort ist der Raum der Abfrage raum_begehen');
  const bild0 = await speichern('vrstart-1-startseite');
  pruef(bildPruefen(bild0), 'Prüfbild: Startseite');

  // ================================================================== Tipp startet VR
  abschnitt('Ein Tipp irgendwo startet die VR-Sitzung genau einmal');
  await ausw(`window.__xr._unterstuetzt = true; window.__xr.dispatchEvent(new Event('devicechange')); return 1;`);
  await naht(`(() => { const v = n.instanz().vr; v.api.anbinden = async () => {}; v.api.kopfPose = () => window.__kopf; v.api.eingabe = () => window.__eingabe; return true; })()`);
  pruef((await anfragen()).length === 0, 'vor dem Tipp keine Sitzungsanfrage');
  await klickAn(1250, 770);   // ganz in der Ecke, weit vom Text
  await klickAn(1250, 770);   // und gleich noch einmal
  await warteBis(() => ausw(`return document.querySelector('${KNOPF}').dataset.status === 'aktiv' && !!document.querySelector('.d3-wurzel')?.dataset.vr && JSON.parse(document.querySelector('.d3-wurzel').dataset.vr).kopf !== null;`), 'VR läuft', 20000);
  await warte(500);
  const an = await anfragen();
  pruef(an.length === 1 && an[0].art === 'immersive-vr', `genau eine Sitzungsanfrage «immersive-vr» (${an.length})`, JSON.stringify(an));
  pruef(an[0].opt.requiredFeatures?.includes('local-floor'), 'mit Referenzraum local-floor', JSON.stringify(an[0].opt));
  const sitzungen = await ausw(`return window.__xr.sitzungen;`);
  pruef(sitzungen === 1, `eine Sitzung (${sitzungen})`);
  const b1 = await ausw(`const w = document.querySelector('.d3-wurzel'); return { begehen: w.dataset.begehen ? JSON.parse(w.dataset.begehen) : null, vr: JSON.parse(w.dataset.vr) };`);
  pruef(b1.begehen?.raum === raum1.kennzeichen, `das Begehen startet im Raum ${raum1.kennzeichen} (${b1.begehen?.raum})`, JSON.stringify(b1.begehen));
  const start = await naht('n.standort()');
  pruef(nahe(b1.begehen.x, start.standpunkt.x, 1) && nahe(b1.begehen.y, start.standpunkt.y, 1), 'am Standpunkt aus raum_begehen (Raummitte)');
  pruef(nahe(b1.vr.kopf.x, start.standpunkt.x / 1000, 0.01) && nahe(b1.vr.kopf.z, -start.standpunkt.y / 1000, 0.01), 'der Kopf steht dort (Meter)', JSON.stringify(b1.vr.kopf));
  const k1 = await knopfInfo();
  pruef(k1.anzeige === 'none', 'die Startseite ist während der Sitzung ausgeblendet');

  // ================================================================== Ende
  abschnitt('Ende der Sitzung: zurück auf der Startseite, Standpunkt gemerkt');
  await ausw(`window.__eingabe.links.y = -1; return 1;`);   // ein paar Schritte vorwärts
  await warte(900);
  await ausw(`window.__eingabe.links.y = 0; return 1;`);
  await warte(400);
  const kopfEnde = await naht('n.instanz().vr.zustand().kopf');
  pruef(Math.hypot(kopfEnde.x - start.standpunkt.x / 1000, kopfEnde.z + start.standpunkt.y / 1000) > 0.3, `der Benutzer ist ein Stück gegangen (Kopf ${kopfEnde.x.toFixed(2)}, ${kopfEnde.z.toFixed(2)})`);
  await ausw(`await window.__xr.sitzung.end(); return 1;`);   // so beendet das Quest-Menü die Sitzung
  await warteBis(() => ausw(`return document.querySelector('${KNOPF}').dataset.status === 'bereit';`), 'Startseite wieder da', 10000);
  await warte(500);
  const k2 = await knopfInfo();
  pruef(k2.status === 'bereit' && /Eintreten/.test(k2.text) && k2.anzeige !== 'none' && nahe(k2.b, k2.fenster.b, 1), 'zurück auf der Startseite mit «Eintreten»', JSON.stringify(k2));
  const oberflaeche2 = await ausw(`return !!document.getElementById('menueband') || !!document.querySelector('.mb-tab');`);
  pruef(!oberflaeche2 && (await ausw(`return location.pathname;`)) === '/vr', 'nicht in der PC-Oberfläche (Adresse /vr)');
  pruef(k2.quelle === 'gemerkt', `die Startseite nennt jetzt den letzten Standpunkt als Quelle (${k2.quelle})`);
  await warteBis(async () => (await einstellung()) !== null, 'Standpunkt in der Benutzereinstellung', 8000);
  const gespeichert = await einstellung();
  pruef(nahe(gespeichert.x, kopfEnde.x * 1000, 60) && nahe(gespeichert.y, -kopfEnde.z * 1000, 60), `gemerkt: (${gespeichert.x}, ${gespeichert.y}) mm ≈ Kopf am Ende (${Math.round(kopfEnde.x * 1000)}, ${Math.round(-kopfEnde.z * 1000)})`, JSON.stringify(gespeichert));
  pruef(gespeichert.raum === raum1.kennzeichen && typeof gespeichert.ort === 'string' && gespeichert.ort.length > 0, 'mit Raum und Projektort', JSON.stringify(gespeichert));
  pruef((await anfragen()).length === 1, 'das Ende hat keine neue Sitzung angefragt');
  const bild1 = await speichern('vrstart-2-nach-ende');
  pruef(bildPruefen(bild1), 'Prüfbild: Startseite nach dem Ende');

  // ================================================================== Neu laden
  abschnitt('Neues Laden nimmt den gemerkten Standpunkt');
  await gehe();
  const k3 = await knopfInfo();
  const st3 = await naht('n.standort()');
  pruef(k3.quelle === 'gemerkt', `Quelle «gemerkt» (${k3.quelle})`, JSON.stringify(k3));
  pruef(st3.standpunkt.x === gespeichert.x && st3.standpunkt.y === gespeichert.y && st3.bodenOk === gespeichert.bodenOk, 'derselbe Standpunkt wie gemerkt');
  pruef(Math.abs(st3.blick.azimutGrad - gespeichert.azimutGrad) < 0.01, `und dieselbe Blickrichtung (${st3.blick.azimutGrad}°)`);
  pruef(k3.raum.includes(raum1.name ?? raum1.kennzeichen) || k3.raum.includes(raum1.kennzeichen), `Raumangabe bleibt: «${k3.raum}»`, JSON.stringify(k3));

  // ================================================================== ?raum=
  abschnitt('?raum=<gültig> hat Vorrang, ?raum=<ungültig> wird ignoriert');
  await gehe(`/vr?raum=${encodeURIComponent(raumOG.id)}`);
  const k4 = await knopfInfo();
  pruef(k4.quelle === 'parameter' && (await naht('n.standort()?.raum')) === raumOG.kennzeichen, `gültiger Raum aus der Adresse: ${raumOG.kennzeichen} (Quelle ${k4.quelle})`, JSON.stringify(k4));
  const og = gs.find((g) => g.kz === raumOG.geschoss);
  pruef(k4.geschoss.includes(og.name ?? og.kz), `Geschoss «${k4.geschoss}»`);
  await gehe(`/vr?raum=${encodeURIComponent(raumOG.kennzeichen)}`);
  pruef((await knopfInfo()).quelle === 'parameter', 'auch das Kennzeichen des Raums geht');
  for (const schlecht of ['%3Cscript%3Ealert(1)%3C%2Fscript%3E', '..%2F..%2Fetc', encodeURIComponent('R 1;x'), '00000000-0000-0000-0000-000000000000', 'RaumGibtEsNicht']) {
    await gehe(`/vr?raum=${schlecht}`);
    const k = await knopfInfo();
    pruef(k.quelle === 'gemerkt' && k.status === 'bereit', `?raum=${decodeURIComponent(schlecht)} wird ignoriert (Quelle ${k.quelle})`, JSON.stringify(k));
  }

  // ================================================================== Ohne gemerkten Standpunkt: Erdgeschoss
  abschnitt('Zurückgesetzter Standpunkt: wieder der erste Raum im Erdgeschoss');
  await ausw(`const r = await fetch('/api/benutzer/einstellungen', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ schluessel: 'vr.letzter_standpunkt', wert: null }) }); return r.status;`);
  await gehe();
  pruef((await knopfInfo()).quelle === 'eg' && (await naht('n.standort()?.raum')) === raum1.kennzeichen, 'ohne gemerkten Standpunkt: erster Raum im Erdgeschoss');

  // ================================================================== VR-Auftrag vom PC
  abschnitt('VR-Auftrag vom PC: /vr zeigt ihn, der Tipp führt ihn aus, ein neuer Auftrag wirkt in der Sitzung ohne Tipp');
  const auftragPost = async (body) => { const r = await fetch(`${SERVER}/api/vr/auftrag`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) }); return { code: r.status, j: await r.json() }; };
  const bad = await auftragPost({ modus: 'begehen', raum: '../../etc' });
  pruef(bad.code === 400 && !!bad.j.fehler, 'eine ungültige Id wird abgelehnt (400)', JSON.stringify(bad));
  pruef((await auftragPost({ modus: 'fliegen' })).code === 400, 'ein unbekannter Modus wird abgelehnt');
  pruef((await holenJson('/api/vr/auftrag')).auftrag == null, 'nichts davon wurde gespeichert');
  const nrPost = async (body) => (await auftragPost(body)).j.auftrag.nr;
  const gOG = modell.elemente.find((e) => e.klasse === 'Geschoss' && e.kennzeichen === raumOG.geschoss);
  const raum2 = raeumeEG[1];
  const standortVon = async (kz) => (await fetch(`${SERVER}/api/abfrage/raum_begehen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ raum: kz }) })).json();
  const naehe = async () => {
    await ausw(`window.__xr._unterstuetzt = true; window.__xr.dispatchEvent(new Event('devicechange')); return 1;`);
    await naht(`(() => { const v = n.instanz().vr; v.api.anbinden = async () => {}; v.api.kopfPose = () => window.__kopf; v.api.eingabe = () => window.__eingabe; return true; })()`);
  };
  const begehenRaum = () => ausw(`const d = document.querySelector('.d3-wurzel')?.dataset.begehen; return d ? JSON.parse(d) : null;`);
  const sitzungZ = () => naht('n.instanz().vr.api.sitzung?.zustand() ?? null');
  const laeuft = () => warteBis(() => ausw(`const d = document.querySelector('.d3-wurzel')?.dataset.vr; return !!d && JSON.parse(d).kopf !== null;`), 'VR läuft', 20000);

  // 1. Auftrag «begehen» im Obergeschoss: die Seite zeigt ihn, der Tipp beginnt dort
  const nr1 = await nrPost({ modus: 'begehen', raum: raumOG.id });
  pruef(nr1 === 1, `Auftrag 1 gesetzt (nr ${nr1})`);
  await gehe();
  const ka = await knopfInfo();
  pruef(ka.quelle === 'auftrag' && /Begehen/.test(ka.text) && ka.text.includes('Auftrag vom PC') && ka.text.includes(raumOG.kennzeichen), `der Knopf zeigt den Auftrag: «${ka.text.slice(0, 160)}»`, JSON.stringify(ka));
  const kaAuftrag = await ausw(`return document.getElementById('vr-auftrag').textContent;`);
  pruef(kaAuftrag.startsWith('Auftrag vom PC: Begehen · ') && kaAuftrag.includes(raumOG.geschoss), `Zeile «${kaAuftrag}»`);
  await speichern('vrstart-4-auftrag');
  await naehe();
  await klickAn(640, 400);
  await laeuft();
  pruef((await anfragen()).length === 1 && (await begehenRaum())?.raum === raumOG.kennzeichen, `der Tipp startet VR im Raum des Auftrags (${raumOG.kennzeichen})`);

  // 2. Neuer Auftrag in der Sitzung: anderer Raum, ohne Tipp
  const nr2 = await nrPost({ modus: 'begehen', raum: raum2.id });
  pruef(await naht('n.takt()'), `Takt: neuer Auftrag (nr ${nr2}) erkannt`);
  await warte(700);
  const st2 = await standortVon(raum2.kennzeichen);
  const b2 = await begehenRaum(), v2 = await ausw(`return JSON.parse(document.querySelector('.d3-wurzel').dataset.vr);`);
  pruef(b2?.raum === raum2.kennzeichen && nahe(b2.x, st2.standpunkt.x, 1) && nahe(b2.y, st2.standpunkt.y, 1), `Standpunkt gewechselt: ${b2?.raum} (ohne neuen Tipp)`, JSON.stringify(b2));
  pruef(nahe(v2.kopf.x, st2.standpunkt.x / 1000, 0.02) && nahe(v2.kopf.z, -st2.standpunkt.y / 1000, 0.02), 'der Kopf steht im neuen Raum', JSON.stringify(v2.kopf));
  pruef((await anfragen()).length === 1, 'dabei keine neue Sitzungsanfrage');
  pruef(await naht('n.instanz().vr.api.modell.parent.children.some((c) => c.isSprite)'), 'Hinweis «Neuer Auftrag vom PC» steht als Etikett in der Szene');
  await warte(2400);
  pruef(!(await naht('n.instanz().vr.api.modell.parent.children.some((c) => c.isSprite)')), 'das Etikett verschwindet nach etwa 2 s');
  pruef(!(await naht('n.takt()')), 'derselbe Auftrag wird nicht noch einmal angewendet');

  // 3. Sitzungsraum mit Etage, dann zurück ins Begehen
  await nrPost({ modus: 'sitzungsraum', geschoss: gOG.id });
  await naht('n.takt()');
  await warteBis(async () => (await sitzungZ())?.aktiv === true && (await sitzungZ())?.geschoss === gOG.kennzeichen, 'Sitzungsraum offen, Etage gesetzt', 8000);
  pruef(true, `Sitzungsraum offen mit Etage ${gOG.kennzeichen}`);
  await nrPost({ modus: 'begehen', raum: raeumeEG[0].id });
  await naht('n.takt()');
  await warteBis(async () => (await sitzungZ())?.aktiv !== true && (await begehenRaum())?.raum === raeumeEG[0].kennzeichen, 'zurück im Begehen', 8000);
  pruef(true, `Begehen statt Sitzungsraum, Raum ${raeumeEG[0].kennzeichen}`);
  await nrPost({ modus: 'sitzungsraum' });   // nur wechseln, ohne Etage
  await naht('n.takt()');
  await warteBis(async () => (await sitzungZ())?.aktiv === true, 'Sitzungsraum ohne Etage', 8000);
  pruef(true, 'Wechsel Begehen › Sitzungsraum ohne neuen Tipp');
  await ausw(`await window.__xr.sitzung.end(); return 1;`);
  await warteBis(() => ausw(`return document.querySelector('${KNOPF}').dataset.status === 'bereit';`), 'Startseite wieder da', 10000);

  // 4. Ein verbrauchter Auftrag kommt nach dem Neuladen nicht wieder
  await gehe();
  const kn = await knopfInfo();
  pruef(kn.quelle === 'gemerkt' && (await ausw(`return document.getElementById('vr-auftrag').textContent;`)) === '', 'nach dem Neuladen steht der verbrauchte Auftrag nicht mehr auf dem Knopf');

  // 5. Auftrag «sitzungsraum» auf der ruhenden Startseite: Anzeige per Takt, der Tipp öffnet den Sitzungsraum mit der Etage
  await nrPost({ modus: 'sitzungsraum', geschoss: gOG.id });
  await naht('n.takt()');
  const kz5 = await knopfInfo();
  const t5 = await ausw(`return document.getElementById('vr-auftrag').textContent;`);
  pruef(t5 === `Auftrag vom PC: Sitzungsraum · ${gOG.kennzeichen}`, `Anzeige «${t5}»`);
  await naehe();
  await klickAn(640, 400);
  await laeuft();
  await warteBis(async () => (await sitzungZ())?.aktiv === true && (await sitzungZ())?.geschoss === gOG.kennzeichen, 'Sitzungsraum mit Etage nach dem Tipp', 10000);
  pruef(true, `der Tipp öffnet den Sitzungsraum mit Etage ${gOG.kennzeichen} (Funktion aus xr-sitzung.js)`);
  await speichern('vrstart-5-sitzungsraum');
  await ausw(`await window.__xr.sitzung.end(); return 1;`);
  await warteBis(() => ausw(`return document.querySelector('${KNOPF}').dataset.status === 'bereit';`), 'Startseite wieder da', 10000);

  // ================================================================== Raum zeigen (VR) und 3D-Ansicht in VR
  abschnitt('Raum zeigen (VR): Auftrag mit Ausschnitt → der Sitzungsraum zeigt nur den Raum plus Rand (Quader aus dem Kern), Massstab passt, Etagenwahl aus');
  /** Prüft in der Seite unabhängig die Ebenen an den Materialien: für jede Ecke der Netz-Boxen stimmt „innerhalb des Quaders“ (eigene Rechnung aus min/max/winkel) mit „alle Ebenen ≥ 0“ überein. */
  const ebenenPruefen = (sb) => naht(`(async () => {
    const T = await import('three'); const v = n.instanz().vr; const m = v.api.modell; m.updateWorldMatrix(true, true);
    const sb = ${JSON.stringify(sb)}; const w = (sb.winkel ?? 0) * Math.PI / 180, c = Math.cos(w), s = Math.sin(w);
    const inv = new T.Matrix4().copy(m.matrixWorld).invert(), rel = new T.Matrix4(), bb = new T.Box3(), q = new T.Vector3(), wp = new T.Vector3();
    const mass = (p) => { const u = p.x * c - p.z * s, vv = -p.x * s - p.z * c; return [u - sb.min[0] / 1000, sb.max[0] / 1000 - u, vv - sb.min[1] / 1000, sb.max[1] / 1000 - vv, p.y - sb.min[2] / 1000, sb.max[2] / 1000 - p.y]; };
    let punkte = 0, abw = 0, bleiben = 0, verschwinden = 0, material = 0, mit6 = 0;
    m.traverse((o) => {
      if (!o.isMesh || !o.geometry) return;
      for (const x of Array.isArray(o.material) ? o.material : [o.material]) { material++; if (x.clippingPlanes?.length === 6) mit6++; }
      const ebenen = (Array.isArray(o.material) ? o.material[0] : o.material).clippingPlanes ?? [];
      if (!o.geometry.boundingBox) o.geometry.computeBoundingBox();
      bb.copy(o.geometry.boundingBox);
      rel.multiplyMatrices(inv, o.matrixWorld);
      const ecken = []; for (const x of [bb.min.x, bb.max.x]) for (const y of [bb.min.y, bb.max.y]) for (const z of [bb.min.z, bb.max.z]) ecken.push(new T.Vector3(x, y, z));
      const ms = ecken.map((e) => mass(q.copy(e).applyMatrix4(rel)));
      const ganzAussen = [0, 1, 2, 3, 4, 5].some((i) => ms.every((mm) => mm[i] < 0));
      if (ganzAussen) verschwinden++; else bleiben++;
      ecken.forEach((e, i) => {
        if (Math.min(...ms[i].map(Math.abs)) < 1e-4) return;   // genau auf einer Fläche: nicht entscheidbar
        punkte++;
        const drin = ms[i].every((d) => d > 0);
        wp.copy(e).applyMatrix4(o.matrixWorld);
        const sichtbar = ebenen.length === 6 && ebenen.every((pl) => pl.distanceToPoint(wp) >= 0);
        if (drin !== sichtbar) abw++;
      });
    });
    return { punkte, abweichungen: abw, bleiben, verschwinden, material, mit6 };
  })()`);
  const rand1500 = (await (await fetch(`${SERVER}/api/abfrage/raum_begehen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ raum: raeumeEG[0].kennzeichen, rand: 1500 }) })).json());
  pruef(rand1500.bereich?.aktiv === true && rand1500.bereich.min.length === 3 && rand1500.standpunkt, 'die Abfrage raum_begehen liefert mit rand den Quader (bereich)', JSON.stringify(rand1500.bereich));
  pruef((await auftragPost({ modus: 'sitzungsraum', raum: raeumeEG[0].id, ausschnitt: 'ja' })).code === 400 && (await auftragPost({ modus: 'sitzungsraum', ausschnitt: true })).code === 400, 'ungültige Ausschnitt-Aufträge werden abgelehnt (400)');
  const nr6 = await nrPost({ modus: 'sitzungsraum', raum: raeumeEG[0].id, ausschnitt: true });
  await gehe();
  const k6 = await ausw(`return document.getElementById('vr-auftrag').textContent;`);
  pruef(k6.startsWith('Auftrag vom PC: Raum zeigen · ') && k6.includes(raeumeEG[0].geschoss), `Zeile «${k6}»`);
  await naehe();
  await klickAn(640, 400);
  await laeuft();
  await warteBis(async () => (await sitzungZ())?.aktiv === true && (await sitzungZ())?.ausschnitt, 'Sitzungsraum mit Ausschnitt', 12000);
  await warte(600);
  const s6 = await sitzungZ();
  pruef(s6.ausschnitt.text.startsWith('Raum ') && s6.ausschnitt.text.includes('1.5 m'), `Beschriftung «${s6.ausschnitt.text}»`);
  pruef(JSON.stringify(s6.ausschnitt.min) === JSON.stringify(rand1500.bereich.min) && JSON.stringify(s6.ausschnitt.max) === JSON.stringify(rand1500.bereich.max) && s6.ausschnitt.winkel === rand1500.bereich.winkel,
    `der Quader ist der des Kerns: min ${JSON.stringify(s6.ausschnitt.min)}, max ${JSON.stringify(s6.ausschnitt.max)}`);
  const ext = (b) => { const w = (b.winkel ?? 0) * Math.PI / 180; const du = (b.max[0] - b.min[0]) / 1000, dv = (b.max[1] - b.min[1]) / 1000; return { b: Math.abs(du * Math.cos(w)) + Math.abs(dv * Math.sin(w)), t: Math.abs(du * Math.sin(w)) + Math.abs(dv * Math.cos(w)), h: (b.max[2] - b.min[2]) / 1000 }; };
  const e6 = ext(rand1500.bereich), erwMass = Math.max(20, Math.ceil(Math.max(e6.b / 1.4, e6.t / 0.8)));
  pruef(s6.massstab === erwMass && e6.b / s6.massstab <= 1.4 + 1e-6 && e6.t / s6.massstab <= 0.8 + 1e-6, `Massstab ${s6.massstabText}: der Ausschnitt (${e6.b.toFixed(1)} × ${e6.t.toFixed(1)} m) passt auf die Platte, so fein wie möglich (erwartet 1:${erwMass})`);
  pruef(s6.geschoss === null && s6.etagen.length >= 3, 'die Etagenwahl ist aus (alle Etagen gelistet, keine gewählt)');
  const tz6 = await naht('n.instanz().vr.api.sitzung.tastenZustand()');
  pruef(tz6 && tz6.alle === false && !Object.keys(tz6).some((k) => k.startsWith('g:') && tz6[k]), '«Alle Etagen» steht nicht auf Ein, solange der Ausschnitt gilt');
  pruef(s6.geclippt === s6.material && s6.material > 0, `alle ${s6.material} Materialien tragen die Ebenen des Ausschnitts`);
  const p6 = await ebenenPruefen(rand1500.bereich);
  pruef(p6.punkte > 100 && p6.abweichungen === 0 && p6.mit6 === p6.material, `die sechs Ebenen schneiden genau am Quader: ${p6.punkte} Ecken geprüft, ${p6.abweichungen} Abweichungen`, JSON.stringify(p6));
  pruef(p6.bleiben > 0 && p6.verschwinden > 0, `nur Körper im Quader bleiben: ${p6.bleiben} Netze im Ausschnitt, ${p6.verschwinden} ganz weggeschnitten`);
  pruef(nahe(s6.modellUnten, rand1500.bereich.min[2] / 1000, 1e-3), `Unterkante des Ausschnitts (${s6.modellUnten} m) steht auf der Platte`);
  await speichern('vrstart-6-raum-zeigen');
  // Raum zeigen live in der Sitzung: ein anderer Raum, ohne Tipp
  await nrPost({ modus: 'sitzungsraum', raum: raeumeEG[1].id, ausschnitt: true });
  await naht('n.takt()');
  await warteBis(async () => JSON.stringify((await sitzungZ())?.ausschnitt?.min) !== JSON.stringify(s6.ausschnitt.min), 'neuer Ausschnitt live', 8000);
  const r2 = await (await fetch(`${SERVER}/api/abfrage/raum_begehen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ raum: raeumeEG[1].kennzeichen, rand: 1500 }) })).json();
  const s6c = await sitzungZ();
  pruef(JSON.stringify(s6c.ausschnitt.min) === JSON.stringify(r2.bereich.min) && JSON.stringify(s6c.ausschnitt.max) === JSON.stringify(r2.bereich.max), 'ein neuer Auftrag in der Sitzung wechselt den Ausschnitt ohne Tipp');
  const p6c = await ebenenPruefen(r2.bereich);
  pruef(p6c.abweichungen === 0 && p6c.punkte > 100, `die Ebenen folgen dem neuen Quader (${p6c.punkte} Ecken, ${p6c.abweichungen} Abweichungen)`);
  // «Alle Etagen» auf dem Tablet: wieder das ganze Modell (über die Taste des Sitzungsraums)
  await naht(`n.instanz().vr.api.sitzung.aktion('alle')`);
  await warte(500);
  const s6b = await sitzungZ();
  pruef(s6b.ausschnitt === null && s6b.massstab === 50 && s6b.geclippt === 0, '«Alle Etagen» zeigt wieder das ganze Modell (1:50, nichts geschnitten)');
  const p6b = await naht(`(() => { let n6 = 0, mm = 0; n.instanz().vr.api.modell.traverse((o) => { for (const x of [].concat(o.material ?? [])) { mm++; if (x.clippingPlanes) n6++; } }); return { mm, n6 }; })()`);
  pruef(p6b.mm > 0 && p6b.n6 === 0, 'keine Ebene mehr an den Materialien');
  // ein einfacher Sitzungsraum-Auftrag zeigt wieder alles
  await nrPost({ modus: 'sitzungsraum' });
  await naht('n.takt()');
  await warteBis(async () => (await sitzungZ())?.ausschnitt === null, 'wieder das ganze Modell', 8000);
  pruef(true, 'ein Auftrag «Sitzungsraum» ohne Raum zeigt im offenen Sitzungsraum wieder das ganze Modell');
  // Die Etagenwahl ersetzt den Ausschnitt
  await nrPost({ modus: 'sitzungsraum', raum: raeumeEG[0].id, ausschnitt: true });
  await naht('n.takt()');
  await warteBis(async () => (await sitzungZ())?.ausschnitt, 'Ausschnitt wieder da', 8000);
  await naht(`n.instanz().vr.api.sitzung.aktion('g:${eg.kz}')`);
  await warte(400);
  const s6d = await sitzungZ();
  pruef(s6d.ausschnitt === null && s6d.geschoss === eg.kz && s6d.ebeneWelt !== null, `die Etage ${eg.kz} ersetzt den Ausschnitt (Etagenebene statt Quader)`);
  await ausw(`await window.__xr.sitzung.end(); return 1;`);
  await warteBis(() => ausw(`return document.querySelector('${KNOPF}').dataset.status === 'bereit';`), 'Startseite wieder da', 10000);

  abschnitt('3D-Ansicht in VR: Auftrag mit ansicht → Schnittbereich und Filter dieser Ansicht auf dem Tisch');
  const post = async (name, body) => (await (await fetch(`${SERVER}/api/befehl/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) })).json());
  // die Klasse mit den wenigsten Körpern (ohne Wand und Decke, die den Rest des Modells tragen) filtert die Test-Ansicht aus
  const klassenZahl = await ausw(`const z = await import('/js/zustand.js'); const v = (await import('/js/vr-start.js')).naht.instanz().vr.api.modell; const k = {}; for (const g of v.children) { const kl = z.element(g.userData.id)?.klasse; if (kl) k[kl] = (k[kl] ?? 0) + 1; } return k;`);
  const klasseAus = Object.entries(klassenZahl).filter(([k]) => !['Wand', 'Decke'].includes(k)).sort((a, b) => a[1] - b[1] || a[0].localeCompare(b[0]))[0]?.[0];
  pruef(!!klasseAus, `Klasse für den Filtertest: ${klasseAus} (Körper je Klasse ${JSON.stringify(klassenZahl)})`);
  const an1 = await post('element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-VRT', name: 'VR-Test', eigenschaften: { art: '3d', klassenAusblenden: [klasseAus] } });
  pruef(an1.angenommen, 'Test-Ansicht AN-VRT (3D, ohne ' + klasseAus + ') angelegt', JSON.stringify(an1).slice(0, 200));
  const rz = await post('ansicht_raum_zeigen', { raum: [raeumeEG[0].kennzeichen], ansicht: 'AN-VRT', rand: 1500 });
  pruef(rz.angenommen, 'Schnittbereich der Ansicht auf den Raum gesetzt (ansicht_raum_zeigen)', JSON.stringify(rz).slice(0, 200));
  const anModell = (await holenJson('/api/modell')).elemente.find((e) => e.kennzeichen === 'AN-VRT');
  const sbAn = anModell.werte.schnittbereich;
  pruef(sbAn?.aktiv === true, 'die Ansicht trägt den aktiven Schnittbereich', JSON.stringify(sbAn));
  const gr = modell.elemente.find((e) => e.klasse === 'Ansicht' && (e.werte?.art ?? 'grundriss') !== '3d');
  const falsch = await auftragPost({ modus: 'sitzungsraum', ansicht: gr.kennzeichen });
  pruef(falsch.code === 400 && /3D-Ansicht/.test(falsch.j.fehler ?? ''), `eine Grundriss-Ansicht (${gr.kennzeichen}) wird abgelehnt: ${falsch.j.fehler}`);
  pruef((await auftragPost({ modus: 'sitzungsraum', ansicht: 'GIBT-ES-NICHT' })).code === 400, 'eine unbekannte Ansicht wird abgelehnt');
  const vorFilter = await naht(`n.instanz().vr.api.modell.children.length`);
  const nr7 = await nrPost({ modus: 'sitzungsraum', ansicht: 'AN-VRT' });
  await gehe();
  const k7 = await ausw(`return document.getElementById('vr-auftrag').textContent;`);
  pruef(k7 === 'Auftrag vom PC: Sitzungsraum · 3D-Ansicht · VR-Test', `Zeile «${k7}»`);
  const gesamt = await naht(`n.instanz().vr.api.modell.children.length`);
  await naehe();
  await klickAn(640, 400);
  await laeuft();
  await warteBis(async () => (await sitzungZ())?.aktiv === true && (await sitzungZ())?.ausschnitt, 'Sitzungsraum mit der Ansicht', 12000);
  await warte(600);
  const s7 = await sitzungZ();
  pruef(JSON.stringify(s7.ausschnitt.min) === JSON.stringify(sbAn.min) && JSON.stringify(s7.ausschnitt.max) === JSON.stringify(sbAn.max), `der Schnittbereich der Ansicht ist der Ausschnitt: ${s7.ausschnitt.text}`);
  const p7 = await ebenenPruefen(sbAn);
  pruef(p7.punkte > 50 && p7.abweichungen === 0 && p7.verschwinden > 0 && p7.bleiben > 0, `Ebenen genau am Quader (${p7.punkte} Ecken, ${p7.abweichungen} Abweichungen; ${p7.bleiben} bleiben, ${p7.verschwinden} weg)`, JSON.stringify(p7));
  const ohneKlasse = await ausw(`const z = await import('/js/zustand.js'); const v = (await import('/js/vr-start.js')).naht.instanz().vr.api.modell; let mit = 0, gesamt = 0; for (const g of v.children) { gesamt++; if (z.element(g.userData.id)?.klasse === ${JSON.stringify(klasseAus)}) mit++; } return { mit, gesamt };`);
  pruef(ohneKlasse.mit === 0 && ohneKlasse.gesamt > 0, `der Filter der Ansicht gilt: ${ohneKlasse.gesamt} Körper, keiner der Klasse ${klasseAus}`, JSON.stringify(ohneKlasse));
  pruef(s7.massstab >= 20 && s7.massstab === Math.max(20, Math.ceil(Math.max(ext(sbAn).b / 1.4, ext(sbAn).t / 0.8))), `Massstab ${s7.massstabText} passt den Ausschnitt auf die Platte`);
  await speichern('vrstart-7-ansicht');
  // ein neuer Auftrag «Sitzungsraum» ohne Ansicht: Gesamtansicht und ganzes Modell zurück
  await nrPost({ modus: 'sitzungsraum' });
  await naht('n.takt()');
  await warteBis(async () => (await sitzungZ())?.ausschnitt === null, 'wieder das ganze Modell', 8000);
  const zurueck = await ausw(`const z = await import('/js/zustand.js'); const v = (await import('/js/vr-start.js')).naht.instanz().vr.api.modell; let mit = 0; for (const g of v.children) if (z.element(g.userData.id)?.klasse === ${JSON.stringify(klasseAus)}) mit++; return { mit, gesamt: v.children.length };`);
  pruef(zurueck.mit > 0 && (await sitzungZ()).massstab === 50, `ohne Ansicht gilt wieder die Gesamtansicht (${zurueck.mit} ${klasseAus}-Körper, 1:50)`);
  pruef(gesamt >= vorFilter - 1, 'die Seite begann mit der Gesamtansicht');
  await ausw(`await window.__xr.sitzung.end(); return 1;`);
  await warteBis(() => ausw(`return document.querySelector('${KNOPF}').dataset.status === 'bereit';`), 'Startseite wieder da', 10000);

  // ================================================================== Manifest und Symbole
  abschnitt('Manifest und Symbole');
  const man = await ausw(`const l = document.querySelector('link[rel="manifest"]'); const r = await fetch(l.href); const j = await r.json();
    const gr = await Promise.all(j.icons.map((i) => new Promise((res) => { const b = new Image(); b.onload = () => res([b.naturalWidth, b.naturalHeight]); b.onerror = () => res(null); b.src = i.src; })));
    return { href: l.getAttribute('href'), status: r.status, typ: r.headers.get('content-type'), j, gr };`);
  pruef(man.status === 200 && /manifest\+json|json/.test(man.typ), `Manifest erreichbar (${man.href}, ${man.typ})`);
  pruef(man.j.start_url === '/vr' && man.j.display === 'standalone' && man.j.name === 'KICAD VR', 'start_url /vr, standalone, Name «KICAD VR»', JSON.stringify(man.j));
  pruef(JSON.stringify(man.gr) === JSON.stringify([[192, 192], [512, 512]]), `Symbole 192 und 512 px als PNG ladbar ${JSON.stringify(man.gr)}`);
  const icon = await ausw(`const l = document.querySelector('link[rel="icon"]'); return !!l && (await fetch(l.href)).ok;`);
  pruef(icon, 'Seitensymbol verlinkt');
  const bild2 = await speichern('vrstart-3-final');
  pruef(bildPruefen(bild2), 'Prüfbild: Startseite');

  pruef(fehler.length === 0, 'keine Skriptfehler in der Seite', fehler.join(' | ').slice(0, 400));
  const wichtig = konsole.filter((m) => !/favicon|404|ResizeObserver|WebGL|GPU stall|swiftshader/i.test(m));
  pruef(wichtig.length === 0, 'keine Fehler in der Konsole', wichtig.join(' | ').slice(0, 400));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e?.stack ?? e);
  try { await speichern('vrstart-abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} ok, ${schlecht} fehlgeschlagen`);
await beenden(schlecht > 0 ? 1 : 0);
