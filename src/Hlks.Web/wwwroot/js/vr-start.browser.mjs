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
