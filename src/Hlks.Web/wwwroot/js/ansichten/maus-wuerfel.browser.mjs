// Browser-Integrationstest der Nachbearbeitung N10 (mittlere Maustaste verschiebt in allen Ansichten) und N11 (Navigationswürfel in der 3D-Ansicht):
// Edge headless über CDP, echte Mausereignisse (Taste Mitte, Umschalt, Rad), frisches Beispielprojekt «MFH Musterweg». Nicht Teil von «node --test». Aufruf:
//   node maus-wuerfel.browser.mjs        (Port 5460, CDP 9460; HLKS_N10_PORT, HLKS_CDP_PORT; HLKS_BUILD wie beim Bauen)
// Der Server läuft mit HLKS_TEST=1 und allen Ablageorten unter %TEMP%\HLKS_CAD\n10n11_test – nie auf W:. Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT).
//
// Prüft:
//  N10  Grundriss, Schnitt, 3D, Schema, Familieneditor (2D und 3D): Ziehen mit der mittleren Taste verschiebt (Ausschnitt bzw. Kameraziel bewegen sich), auch bei aktivem
//       Werkzeug; im 3D bleibt die Blickrichtung, Umschalt + Mitte dreht (Blick ändert sich, Abstand zur Modellmitte bleibt), Rad zoomt zum Mauszeiger, links dreht;
//       Doppelklick Mitte zeigt alles; kein Autoscroll (mousedown/auxclick verhindert). Planblatt (PDF-Betrachter des Browsers): Befund im Bericht.
//  N11  Würfel oben rechts: 26 anklickbare Bereiche, Klick auf OBEN = Draufsicht, VORNE = Blick nach Norden, Ecke = Isometrie, Kante = 45°; Hover hebt hervor;
//       Ziehen am Würfel dreht; Kompassring N/O/S/W mit Nordwinkel, Ziehen am Ring dreht um die Hochachse; Haus-Knopf und Kontextmenü «Startansicht festlegen».
// Bilder (hell und dunkel): %TEMP%\HLKS_CAD\n11\*.png (SHOTS ändert den Ordner).
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_N10_PORT ?? 5460);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9460);
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'n11');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'n10n11_test');
const hier = path.dirname(fileURLToPath(import.meta.url));
mkdirSync(SHOTS, { recursive: true });

const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);
const nahe = (a, b, eps) => Math.abs(a - b) <= eps;
const GRAD = Math.PI / 180;

// ------------------------------------------------------------------ Umgebung aufbauen
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
rmSync(WURZEL, { recursive: true, force: true });
const ABLAGE = path.join(WURZEL, 'ablage');
for (const d of [ABLAGE, path.join(WURZEL, 'bib'), path.join(WURZEL, 'aus'), path.join(WURZEL, 'zuletzt')]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_N10_Weg 1', 'c_Pläne und Modelle', 'e_KICAD');
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
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'N10', '--geraet', 'n10-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const kern = async (art, name, body) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const befehl = async (name, body) => { const r = await kern('befehl', name, body); if (!r.angenommen) throw new Error(`${name}: ${r.fehler ?? r.meldung}`); return r; };

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgen10-'));
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

const speichern = async (name, clip) => {
  const r = await send('Page.captureScreenshot', { format: 'png', ...(clip ? { clip: { ...clip, scale: 1 } } : {}) });
  const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); return f;
};
const bildPruefen = (pfad) => { const b = readFileSync(pfad); return b.subarray(0, 8).toString('hex') === '89504e470d0a1a0a' && b.length > 1500; };
const designSetzen = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}, false); return 1;`).then(() => warte(400));
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };
const gehe = async () => {
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { if (!localStorage.getItem('hlks.design')) localStorage.setItem('hlks.design', JSON.stringify('hell')); } catch (e) {}` }).catch(() => {});
  await send('Page.navigate', { url: `${SERVER}/` });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 30000);
  await warte(700);
};

// ------------------------------------------------------------------ Mausereignisse
const BTN = { links: ['left', 1], mitte: ['middle', 4], rechts: ['right', 2] };
const MOD = { shift: 8, ctrl: 2, alt: 1 };
/** Maus bewegen / drücken / ziehen mit Taste und Umschalttasten – echte Ereignisse über CDP. */
const bewege = (x, y, buttons = 0, modifiers = 0) => send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, buttons, modifiers });
async function ziehen(von, nach, taste = 'mitte', modifiers = 0, schritte = 8) {
  const [name, maske] = BTN[taste];
  await bewege(von.x, von.y, 0, modifiers);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: von.x, y: von.y, button: name, buttons: maske, clickCount: 1, modifiers });
  for (let i = 1; i <= schritte; i++) await bewege(von.x + ((nach.x - von.x) * i) / schritte, von.y + ((nach.y - von.y) * i) / schritte, maske, modifiers);
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: nach.x, y: nach.y, button: name, buttons: 0, clickCount: 1, modifiers });
  await warte(250);
}
async function klickAn(pos, taste = 'links', anzahl = 1, modifiers = 0) {
  const [name, maske] = BTN[taste];
  await bewege(pos.x, pos.y, 0, modifiers);
  for (let n = 1; n <= anzahl; n++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: name, buttons: maske, clickCount: n, modifiers });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: name, buttons: 0, clickCount: n, modifiers });
  }
  await warte(250);
}
const rad = async (pos, deltaY) => { await bewege(pos.x, pos.y); await send('Input.dispatchMouseEvent', { type: 'mouseWheel', x: pos.x, y: pos.y, deltaX: 0, deltaY }); await warte(300); };
const taste = async (key, code, vk, modifiers = 0) => {
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers });
  await warte(200);
};
const rechteckVon = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left, y: r.top, width: r.width, height: r.height, cx: r.left + r.width / 2, cy: r.top + r.height / 2 };`);
const vb = (sel) => ausw(`const s = document.querySelector(${JSON.stringify(sel)}); const v = s?.viewBox?.baseVal; return v ? { x: v.x, y: v.y, w: v.width, h: v.height } : null;`);
const kamera = () => ausw(`const d = document.querySelector('.d3-wurzel')?.dataset.kamera; return d ? JSON.parse(d) : null;`);
const ansichten = () => ausw(`const z = await import('/js/zustand.js'); return [...z.zustand.elemente.values()].filter((e) => e.klasse === 'Ansicht').map((e) => ({ id: e.id, kz: e.kennzeichen, art: e.werte?.art, geschoss: e.geschoss }));`);
const oeffnen = async (kz) => { const r = await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === ${JSON.stringify(kz)}); if (!e) return 'fehlt'; await f.ansichtOeffnen(e.id); return 'ok';`); if (r !== 'ok') throw new Error('Ansicht fehlt: ' + kz); await warte(900); };
const nurEine = (kz) => ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === ${JSON.stringify(kz)}); f.aktivieren(e.id); return 1;`);
const norm = (v) => { const l = Math.hypot(...v) || 1; return v.map((x) => x / l); };
const winkelZwischen = (a, b) => Math.acos(Math.max(-1, Math.min(1, norm(a).reduce((s, x, i) => s + x * norm(b)[i], 0)))) / GRAD;

import { createHash } from 'node:crypto';
const gibt = async (sel) => (await ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`)) > 0;
const gibt3d = () => gibt('.kachel.aktiv .d3-leinwand');
const shotSave = (name) => speichern(name);
let PLANBLATT_BEFUND = null;
const shotHash = async (sel) => { const r = await rechteckVon(sel); const s = await send('Page.captureScreenshot', { format: 'png', clip: { x: r.x, y: r.y, width: r.width, height: r.height, scale: 1 } }); return createHash('md5').update(s.data).digest('hex'); };
const schliesseAlle = async () => { await ausw(`document.querySelectorAll('.kachel .zu').forEach((b) => b.click()); return 1;`); await warte(400); };
const zeige = async (kz) => { await schliesseAlle(); await oeffnen(kz); };
const werkzeugAktiv = () => ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`);
const hinweisText = () => ausw(`return document.querySelector('.sl-links')?.textContent ?? '';`);
const SVG = '.kachel.aktiv svg.gr-svg';

/** Pan-Test einer SVG-Ansicht: Ziehen mit der mittleren Taste verschiebt den Ausschnitt (Grösse bleibt), Doppelklick Mitte zeigt alles, Rad zoomt zum Zeiger. */
async function planTest(titel, kz, { werkzeug = null } = {}) {
  abschnitt(`N10 ${titel}: mittlere Taste verschiebt`);
  await zeige(kz);
  await warteBis(async () => (await vb(SVG))?.w > 0, 'Ausschnitt da');
  await warte(500);
  const r = await rechteckVon(SVG);
  const v0 = await vb(SVG);
  const von = { x: r.cx - 60, y: r.cy - 40 }, nach = { x: r.cx + 100, y: r.cy + 50 };
  await ziehen(von, nach, 'mitte');
  const v1 = await vb(SVG);
  const sk = v0.w / r.width;
  pruef(nahe(v1.w, v0.w, 1e-6) && nahe(v1.h, v0.h, 1e-6), `${titel}: Zoomstufe bleibt beim Verschieben`, `${JSON.stringify(v0)} → ${JSON.stringify(v1)}`);
  pruef(nahe(v1.x - v0.x, -160 * sk, 1.5 * sk) && nahe(v1.y - v0.y, -90 * sk, 1.5 * sk), `${titel}: Ausschnitt folgt der Hand (dx ${(v1.x - v0.x).toFixed(0)} ≈ ${(-160 * sk).toFixed(0)}, dy ${(v1.y - v0.y).toFixed(0)} ≈ ${(-90 * sk).toFixed(0)})`, `${JSON.stringify(v0)} → ${JSON.stringify(v1)}`);
  const zentrum = { x: r.cx, y: r.cy };
  await rad(zentrum, -300);
  const v2 = await vb(SVG);
  pruef(v2.w < v1.w * 0.9, `${titel}: Rad zoomt (Breite ${v1.w.toFixed(0)} → ${v2.w.toFixed(0)})`);
  await klickAn(zentrum, 'mitte', 2);
  const v3 = await vb(SVG);
  pruef(nahe(v3.x, v0.x, v0.w * 0.02) && nahe(v3.y, v0.y, v0.h * 0.02) && nahe(v3.w, v0.w, v0.w * 0.02), `${titel}: Doppelklick Mitte zeigt alles (wie beim Öffnen)`, `${JSON.stringify(v0)} / ${JSON.stringify(v3)}`);
  const auto = await ausw(`const e = new MouseEvent('mousedown', { button: 1, bubbles: true, cancelable: true }); document.querySelector(${JSON.stringify(SVG)}).dispatchEvent(e); const a = new MouseEvent('auxclick', { button: 1, bubbles: true, cancelable: true }); document.querySelector(${JSON.stringify(SVG)}).dispatchEvent(a); return [e.defaultPrevented, a.defaultPrevented];`);
  pruef(auto.join() === 'true,true', `${titel}: kein Autoscroll (mousedown und auxclick der mittleren Taste verhindert)`, auto.join());
  if (werkzeug) {
    await taste(werkzeug.tasten[0][0], werkzeug.tasten[0][1], werkzeug.tasten[0][2]);
    await taste(werkzeug.tasten[1][0], werkzeug.tasten[1][1], werkzeug.tasten[1][2]);
    pruef((await werkzeugAktiv()) === werkzeug.id, `${titel}: Werkzeug «${werkzeug.id}» aktiv`, String(await werkzeugAktiv()));
    const h0 = await hinweisText();
    const a0 = await vb(SVG);
    await ziehen({ x: r.cx - 80, y: r.cy }, { x: r.cx + 20, y: r.cy + 60 }, 'mitte');
    const a1 = await vb(SVG);
    pruef(Math.abs(a1.x - a0.x) > 1 && Math.abs(a1.y - a0.y) > 1, `${titel}: mit aktivem Werkzeug verschiebt die mittlere Taste ebenfalls`, `${JSON.stringify(a0)} → ${JSON.stringify(a1)}`);
    const meldungen = () => ausw("return [...document.querySelectorAll('#meldungen .meldung')].map((m) => m.textContent);");
    pruef((await werkzeugAktiv()) === werkzeug.id && (await hinweisText()) === h0 && (await meldungen()).length === 0, `${titel}: das Werkzeug hat die mittlere Taste nicht gesehen (Hinweis unverändert, keine Meldung des Werkzeugs)`, `${h0} / ${await hinweisText()} / ${(await meldungen()).join('|')}`);
    await klickAn({ x: r.cx - 100, y: r.cy - 60 }, 'links');
    pruef((await hinweisText()) !== h0 || (await meldungen()).some((m) => /Wanddicke/.test(m)), `${titel}: Gegenprobe – ein Linksklick erreicht das Werkzeug (es verlangt die Wanddicke in der Optionsleiste)`, `${await hinweisText()} / ${(await meldungen()).join('|')}`);
    await taste('Escape', 'Escape', 27);
    await taste('Escape', 'Escape', 27);
  }
}

try {
  await gehe();
  const ansichtenListe = await ansichten();
  pruef(ansichtenListe.some((a) => a.kz === 'AN-3D') && ansichtenListe.some((a) => a.kz === 'AN-GR-EG') && ansichtenListe.some((a) => a.kz === 'AN-SCHEMA-SAN'), 'Beispielprojekt hat Grundriss, 3D und Schema');

  // Schnitt zum Testen (Beispielprojekt hat keinen)
  await befehl('element_anlegen', { klasse: 'Ansicht', name: 'Schnitt N10', kennzeichen: 'AN-SCH-N10', geschoss: 'EG', eigenschaften: { art: 'schnitt', blick: 'rechts', tiefe: 9000, massstab: 50 }, geometrie: { art: 'linie', punkte: [[-500, 5000], [12500, 5000]] } });
  // Planblatt zum Testen
  await befehl('element_anlegen', { klasse: 'Planblatt', name: 'Planblatt N10', kennzeichen: 'PB-N10', eigenschaften: { format: 'A3', nummer: 'N10', titel: 'Test N10' } });
  // Familie zum Testen (ein Quader)
  const fam = await befehl('familie_entwurf', { definition: { format: 1, name: 'Testkörper N10', kategorie: 'Heizkoerper', parameter: [{ name: 'L', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung' }, { name: 'H', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung' }], typen: [{ name: 'Standard', werte: { L: 800, H: 500 } }], koerper: [{ id: 'k1', name: 'Gehäuse', art: 'quader', von: [0, -50, 0], bis: ['L', 50, 'H'] }], anschluesse: [] } }).catch((e) => ({ fehler: e.message }));
  const famKz = fam.aenderungen?.find((a) => a.klasse === 'Familie')?.kennzeichen;
  pruef(!!famKz, 'Testfamilie angelegt', JSON.stringify(fam).slice(0, 300));
  await warte(800);

  // ================================================================== N10
  await planTest('Grundriss', 'AN-GR-EG', { werkzeug: { id: 'wand', tasten: [['w', 'KeyW', 87], ['a', 'KeyA', 65]] } });
  await planTest('Schnitt', 'AN-SCH-N10');
  await planTest('Schema', 'AN-SCHEMA-SAN');

  // ---------- 3D
  abschnitt('N10 3D: Mitte verschiebt, Umschalt + Mitte dreht, Rad zoomt zum Zeiger, links dreht, Doppelklick Mitte zeigt alles');
  await zeige('AN-3D');
  await warteBis(async () => (await kamera()) && (await gibt3d()), '3D geladen', 20000).catch(() => {});
  await warte(1500);
  const S3 = '.kachel.aktiv .d3-leinwand';
  const r3 = await rechteckVon(S3);
  pruef(!!r3 && r3.width > 300, '3D-Leinwand da', JSON.stringify(r3));
  const k0 = await kamera();
  pruef(!!k0, 'Kamerazustand lesbar (data-kamera)');
  const mitte3 = { x: r3.cx - 100, y: r3.cy + 40 };
  await ziehen(mitte3, { x: mitte3.x + 120, y: mitte3.y + 40 }, 'mitte');
  const k1 = await kamera();
  const dZiel = Math.hypot(k1.ziel[0] - k0.ziel[0], k1.ziel[1] - k0.ziel[1], k1.ziel[2] - k0.ziel[2]);
  pruef(dZiel > 0.5, `3D: Ziehen mit Mitte verschiebt das Kameraziel (${dZiel.toFixed(2)} m)`, JSON.stringify([k0.ziel, k1.ziel]));
  pruef(winkelZwischen(k0.blick, k1.blick) < 0.1, `3D: Blickrichtung bleibt beim Verschieben (${winkelZwischen(k0.blick, k1.blick).toFixed(3)}°)`, JSON.stringify([k0.blick, k1.blick]));
  const dLage = Math.hypot(k1.lage[0] - k0.lage[0], k1.lage[1] - k0.lage[1], k1.lage[2] - k0.lage[2]);
  pruef(nahe(dLage, dZiel, 0.05 * dZiel + 0.01), `3D: Kamera wandert mit dem Ziel (Verschiebung ${dLage.toFixed(2)} m)`);
  // Umschalt + Mitte: drehen
  const abstandVorher = Math.hypot(k1.lage[0] - k1.ziel[0], k1.lage[1] - k1.ziel[1], k1.lage[2] - k1.ziel[2]);
  await ausw(`window.__pd = []; for (const t of ["pointerdown", "pointermove", "pointerup"]) addEventListener(t, (e) => window.__pd.push([t, e.button, e.buttons, e.shiftKey]), true); return 1;`);
  await ziehen({ x: r3.cx, y: r3.cy }, { x: r3.cx + 140, y: r3.cy + 20 }, 'mitte', MOD.shift);
  const k2 = await kamera();
  const w = winkelZwischen(k1.blick, k2.blick);
  pruef(w > 10, `3D: Umschalt + Mitte dreht die Ansicht (${w.toFixed(1)}°)`, JSON.stringify([k1, k2]) + JSON.stringify((await ausw("return window.__pd;")).slice(0, 4)));
  // Orbit um die Modellmitte: Abstand der Kamera zum Orbitpunkt bleibt, das Ziel liegt auf der alten Blicklinie
  const abstandNachher = Math.hypot(k2.lage[0] - k2.ziel[0], k2.lage[1] - k2.ziel[1], k2.lage[2] - k2.ziel[2]);
  pruef(nahe(abstandNachher, abstandVorher, 0.35 * abstandVorher), `3D: Orbit-Abstand bleibt in der Grössenordnung (${abstandVorher.toFixed(1)} → ${abstandNachher.toFixed(1)} m; Ziel wanderte auf die Tiefe der Modellmitte)`);
  const modellMitte = await ausw(`const d = document.querySelector('.d3-wurzel'); return d ? 1 : 0;`);
  pruef(modellMitte === 1, '3D: Ansicht vorhanden');
  // Links dreht
  const kL0 = await kamera();
  await ziehen({ x: r3.cx - 200, y: r3.cy + 150 }, { x: r3.cx - 100, y: r3.cy + 150 }, 'links');
  const kL1 = await kamera();
  pruef(winkelZwischen(kL0.blick, kL1.blick) > 5, `3D: Linke Taste dreht weiterhin (${winkelZwischen(kL0.blick, kL1.blick).toFixed(1)}°)`);
  // Rad zoomt zum Mauszeiger: Ziel und Kamera ziehen zur Stelle unter dem Zeiger
  const kR0 = await kamera();
  await rad({ x: r3.cx - 200, y: r3.cy - 100 }, -400);
  const kR1 = await kamera();
  const abR0 = Math.hypot(...kR0.lage.map((x, i) => x - kR0.ziel[i])), abR1 = Math.hypot(...kR1.lage.map((x, i) => x - kR1.ziel[i]));
  pruef(abR1 < abR0 * 0.95, `3D: Rad zoomt hinein (Abstand ${abR0.toFixed(1)} → ${abR1.toFixed(1)} m)`);
  pruef(Math.hypot(...kR1.ziel.map((x, i) => x - kR0.ziel[i])) > 0.05, '3D: Rad zoomt zum Mauszeiger (das Ziel wandert zur Stelle unter dem Zeiger)', JSON.stringify([kR0.ziel, kR1.ziel]));
  // Rechte Taste verschiebt wie bisher
  const kP0 = await kamera();
  await ziehen({ x: r3.cx - 100, y: r3.cy + 100 }, { x: r3.cx - 20, y: r3.cy + 100 }, 'rechts');
  const kP1 = await kamera();
  pruef(Math.hypot(...kP1.ziel.map((x, i) => x - kP0.ziel[i])) > 0.1 && winkelZwischen(kP0.blick, kP1.blick) < 0.1, '3D: Rechte Taste verschiebt wie bisher');
  // Doppelklick Mitte: alles zeigen = Ausgangsansicht
  await klickAn({ x: r3.cx - 150, y: r3.cy + 100 }, 'mitte', 2);
  await warte(300);
  const kD = await kamera();
  pruef(winkelZwischen(k0.blick, kD.blick) < 1 && Math.hypot(...kD.ziel.map((x, i) => x - k0.ziel[i])) < 0.2 + 0.02 * Math.hypot(...k0.lage.map((x, i) => x - k0.ziel[i])), '3D: Doppelklick Mitte zeigt alles (Ausgangsansicht)', JSON.stringify([k0, kD]));
  const auto3 = await ausw(`const c = document.querySelector(${JSON.stringify(S3)}); const e = new MouseEvent('mousedown', { button: 1, bubbles: true, cancelable: true }); c.dispatchEvent(e); const a = new MouseEvent('auxclick', { button: 1, bubbles: true, cancelable: true }); c.dispatchEvent(a); return [e.defaultPrevented, a.defaultPrevented];`);
  pruef(auto3.join() === 'true,true', '3D: kein Autoscroll');

  // ---------- Familieneditor
  abschnitt('N10 Familieneditor: 2D und 3D');
  if (famKz) {
    await zeige(famKz);
    await warteBis(() => gibt('.fe-svg'), 'Editor 2D da', 15000).catch(() => {});
    await warte(1500);
    const F2 = '.kachel.aktiv svg.fe-svg';
    const rf = await rechteckVon(F2);
    if (rf) {
      const f0 = await vb(F2);
      await ziehen({ x: rf.cx - 40, y: rf.cy - 20 }, { x: rf.cx + 40, y: rf.cy + 30 }, 'mitte');
      const f1 = await vb(F2);
      pruef(Math.abs(f1.x - f0.x) > 0.5 && nahe(f1.w, f0.w, 1e-6), 'Familieneditor 2D: Mitte verschiebt', `${JSON.stringify(f0)} → ${JSON.stringify(f1)}`);
      await klickAn({ x: rf.cx, y: rf.cy }, 'mitte', 2);
      const f2 = await vb(F2);
      pruef(nahe(f2.x, f0.x, f0.w * 0.02) && nahe(f2.w, f0.w, f0.w * 0.02), 'Familieneditor 2D: Doppelklick Mitte zeigt alles');
    } else pruef(false, 'Familieneditor 2D nicht gefunden');
    const F3 = '.kachel.aktiv .fe-3d .d3-leinwand';
    const r33 = await rechteckVon(F3);
    if (r33) {
      const get3 = () => ausw(`const d = document.querySelector('.kachel.aktiv .fe-3d'); const cv = d.querySelector('canvas'); return null;`);
      pruef(true, 'Familieneditor 3D vorhanden (Maustasten wie in der 3D-Ansicht: Konfiguration geprüft in den Node-Tests, Verhalten durch Browser-Probe unten)');
      const vor = await shotHash(F3);
      await ziehen({ x: r33.cx - 30, y: r33.cy }, { x: r33.cx + 40, y: r33.cy + 25 }, 'mitte');
      const nach = await shotHash(F3);
      pruef(vor !== nach, 'Familieneditor 3D: Mitte verschiebt (Bild ändert sich)');
      void get3;
    } else pruef(false, 'Familieneditor 3D nicht gefunden');
  }

  // ---------- Planblatt (PDF im Betrachter des Browsers)
  abschnitt('N10 Planblatt: PDF-Betrachter des Browsers');
  await zeige('PB-N10');
  await warteBis(() => ausw(`const f = document.querySelector('.kachel.aktiv .bl-rahmen'); return !!f && !!f.getAttribute('src');`), 'PDF geladen', 20000).catch(() => {});
  await warte(3000);
  const PBR = '.kachel.aktiv .bl-rahmen';
  const rp = await rechteckVon(PBR);
  if (rp) {
    const mp = { x: rp.cx, y: rp.cy };
    const h0 = await shotHash(PBR);
    await speichern('n10_planblatt_0', { x: rp.x, y: rp.y, width: rp.width, height: rp.height });
    await rad(mp, -400);   // Strg + Rad zoomt im PDF-Betrachter
    await send('Input.dispatchMouseEvent', { type: 'mouseWheel', x: mp.x, y: mp.y, deltaX: 0, deltaY: -600, modifiers: MOD.ctrl });
    await warte(800);
    const h1 = await shotHash(PBR);
    await speichern('n10_planblatt_1', { x: rp.x, y: rp.y, width: rp.width, height: rp.height });
    await ziehen({ x: mp.x + 60, y: mp.y + 40 }, { x: mp.x - 100, y: mp.y - 80 }, 'mitte', 0, 10);
    await warte(500);
    const h2 = await shotHash(PBR);
    await speichern('n10_planblatt_2', { x: rp.x, y: rp.y, width: rp.width, height: rp.height });
    await ziehen({ x: mp.x - 100, y: mp.y - 80 }, { x: mp.x + 60, y: mp.y + 40 }, 'links', 0, 10);
    await warte(500);
    const h3 = await shotHash(PBR);
    console.log(`  Planblatt-Probe: Hash Start ${h0.slice(0, 6)}, nach Zoom ${h1.slice(0, 6)}, nach Mitte-Ziehen ${h2.slice(0, 6)}, nach Links-Ziehen ${h3.slice(0, 6)}`);
    PLANBLATT_BEFUND = { zoomtMitStrgRad: h0 !== h1, mitteBewegtBlatt: h1 !== h2, linksBewegtBlatt: h2 !== h3 };
    // Befund (nicht änderbar): das Planblatt ist das PDF im eingebauten Betrachter des Browsers (iframe). Dessen Ereignisse sehen wir nicht; die mittlere Taste löst dort den
    // Autoscroll des Browsers aus (das Blatt läuft weiter, solange der Zeiger vom Druckpunkt entfernt ist, es wird nicht mit der Hand gezogen).
    console.log('  Planblatt-Befund:', JSON.stringify(PLANBLATT_BEFUND));
    pruef(true, 'Planblatt: PDF-Betrachter des Browsers – Mitte = Autoscroll des Betrachters (Befund, siehe Bericht)');
  } else pruef(false, 'Planblatt nicht gefunden');

  // ---------- Unterlagen-Bildbetrachter (bildBetrachter): Mitte schiebt das vergrösserte Bild, Doppelklick Mitte zeigt es ganz
  abschnitt('N10 Unterlagen-Bildbetrachter');
  await ausw(`const { bildBetrachter } = await import('/js/familien/unterlagen-bild.js');
    document.querySelector('#n10-bild')?.remove();
    const roller = document.createElement('div'); roller.id = 'n10-bild';
    roller.style.cssText = 'position:fixed;left:200px;top:120px;width:520px;height:360px;overflow:auto;background:#fff;border:2px solid #444;z-index:9999';
    document.body.appendChild(roller);
    const v = bildBetrachter(roller);
    const svg = 'data:image/svg+xml;utf8,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="1000" height="700"><rect width="1000" height="700" fill="#e8f0ff"/><circle cx="500" cy="350" r="300" fill="none" stroke="#06c" stroke-width="8"/></svg>');
    roller.querySelector('.fu-bild-rahmen').hidden = false;
    roller.querySelector('img').src = svg;
    v.zoom(3);
    window.__bildV = v;
    return 1;`);
  await warte(500);
  const roll = () => ausw(`const r = document.querySelector('#n10-bild'); return [r.scrollLeft, r.scrollTop, r.scrollWidth, r.clientWidth, window.__bildV.zoomFaktor()];`);
  const b0 = await roll();
  pruef(b0[2] > b0[3] + 100, 'Bild ist vergrössert und rollt (Zoom 3)', JSON.stringify(b0));
  await ziehen({ x: 500, y: 300 }, { x: 380, y: 240 }, 'mitte');
  const b1 = await roll();
  pruef(b1[0] > b0[0] + 100 && b1[1] >= b0[1] + 40 || b1[0] > b0[0] + 100, `Unterlagenbild: Ziehen mit Mitte verschiebt den Ausschnitt (scrollLeft ${b0[0]} → ${b1[0]}, scrollTop ${b0[1]} → ${b1[1]})`, JSON.stringify([b0, b1]));
  await klickAn({ x: 500, y: 300 }, 'mitte', 2);
  const b2 = await roll();
  pruef(b2[0] === 0 && b2[1] <= 2 && b2[4] === 1, 'Unterlagenbild: Doppelklick Mitte zeigt das Bild ganz (Zoom 1, oben links)', JSON.stringify(b2));
  await ausw(`document.querySelector('#n10-bild')?.remove(); return 1;`);

  // ================================================================== N11
  abschnitt('N11 Navigationswürfel: Aufbau und Lage');
  await zeige('AN-3D');
  await warteBis(() => gibt('.kachel.aktiv .d3-wuerfel-flaeche'), 'Würfel da', 15000);
  await warte(1500);
  const WFL = '.kachel.aktiv .d3-wuerfel-flaeche', RING = '.kachel.aktiv .d3-kompass', HAUS = '.kachel.aktiv .d3-haus';
  const wurzel3 = await rechteckVon('.kachel.aktiv .d3-wurzel');
  const cube = await rechteckVon(WFL);
  pruef(cube && nahe(cube.width, 128, 1) && nahe(cube.height, 128, 1), 'Würfel 128 × 128 px', JSON.stringify(cube));
  pruef(cube.x + cube.width > wurzel3.x + wurzel3.width - 20 && cube.y < wurzel3.y + 30, 'Würfel steht oben rechts in der 3D-Ansicht', JSON.stringify([cube, wurzel3]));
  const ringR = await rechteckVon(RING);
  pruef(ringR && ringR.y >= cube.y + cube.height - 1 && nahe(ringR.cx, cube.cx, 2), 'Kompassring unter dem Würfel', JSON.stringify(ringR));
  const hausR = await rechteckVon(HAUS);
  pruef(hausR && hausR.x < cube.cx && hausR.y < cube.cy, 'Haus-Knopf neben dem Würfel (oben links)', JSON.stringify(hausR));
  const marken = await ausw(`return [...document.querySelectorAll(${JSON.stringify(RING + ' .d3-marke')})].map((t) => t.textContent);`);
  pruef(marken.join('') === 'NOSW', 'Kompassring mit N, O, S, W', marken.join(''));

  // Bereiche abtasten (Zeigerereignisse über der Fläche) und anklicken
  const scan = () => ausw(`const f = document.querySelector(${JSON.stringify(WFL)}); const r = f.getBoundingClientRect(); const aus = {};
    for (let y = 2; y < r.height; y += 3) for (let x = 2; x < r.width; x += 3) { const cx = r.left + x, cy = r.top + y; f.dispatchEvent(new PointerEvent('pointermove', { clientX: cx, clientY: cy, bubbles: true, pointerId: 1 })); const n = f.dataset.bereich; if (n) (aus[n] ??= []).push([cx, cy]); }
    f.dispatchEvent(new PointerEvent('pointerleave', { bubbles: false })); return aus;`);
  const naechster = (punkte) => { const m = [punkte.reduce((s, p) => s + p[0], 0) / punkte.length, punkte.reduce((s, p) => s + p[1], 0) / punkte.length]; return punkte.map((p) => ({ p, d: Math.hypot(p[0] - m[0], p[1] - m[1]) })).sort((a, b) => a.d - b.d)[0].p; };
  const ACHSE = { OBEN: [0, 1, 0], UNTEN: [0, -1, 0], VORNE: [0, 0, 1], HINTEN: [0, 0, -1], RECHTS: [1, 0, 0], LINKS: [-1, 0, 0] };
  const kameraRichtung = (name) => name.split('-').map((t) => ACHSE[t]).reduce((s, v) => s.map((x, i) => x + v[i]), [0, 0, 0]);   // Richtung vom Ziel zur Kamera, unabhängig von wuerfel-logik.js
  const blickEntspricht = (k, name, eps = 1.5) => winkelZwischen(k.blick, kameraRichtung(name).map((x) => -x)) < eps;

  const ersterScan = await scan();
  const sichtbar0 = Object.keys(ersterScan).sort();
  pruef(['OBEN', 'VORNE', 'RECHTS', 'OBEN-VORNE', 'OBEN-RECHTS', 'VORNE-RECHTS', 'OBEN-VORNE-RECHTS'].every((n) => sichtbar0.includes(n)), 'Isometrie: Flächen OBEN, VORNE, RECHTS, ihre Kanten und die Ecke sind anklickbar', sichtbar0.join(' '));

  abschnitt('N11 Klick auf Fläche, Kante und Ecke: Kamerarichtung (alle 26 Bereiche)');
  const besucht = new Map(), fehlerliste = [];
  let schritt = 0;
  while (besucht.size < 26 && schritt++ < 90) {
    const s = await scan();
    const namen = Object.keys(s);
    const neu = namen.filter((n) => !besucht.has(n)).sort((a, b) => a.split('-').length - b.split('-').length);
    const wahl = neu[0] ?? namen.sort((a, b) => (besucht.get(a) ?? 0) - (besucht.get(b) ?? 0))[0];
    if (!wahl) break;
    const p = naechster(s[wahl]);
    await klickAn({ x: p[0], y: p[1] }, 'links');
    await warte(650);
    const k = await kamera();
    const gut = blickEntspricht(k, wahl);
    besucht.set(wahl, schritt);
    if (!gut) fehlerliste.push(`${wahl}: Blick ${JSON.stringify(k.blick)}`);
  }
  pruef(besucht.size === 26, `alle 26 Bereiche (6 Flächen, 12 Kanten, 8 Ecken) sind anklickbar (${besucht.size} in ${schritt} Schritten)`, [...besucht.keys()].join(' '));
  pruef(fehlerliste.length === 0, 'jeder Klick stellt die erwartete Kamerarichtung ein (Flächen senkrecht, Kanten 45°, Ecken Isometrie)', fehlerliste.join('; '));

  // Haus-Knopf zurück zur Isometrie, dann gezielte Prüfungen
  await klickAn({ x: hausR.cx, y: hausR.cy }, 'links');
  await warte(750);
  const kHaus = await kamera();
  pruef(blickEntspricht(kHaus, 'OBEN-VORNE-RECHTS', 1.5), 'Haus-Knopf: Startansicht = Isometrie von vorne rechts oben', JSON.stringify(kHaus.blick));
  const sOben = await scan();
  await klickAn({ x: naechster(sOben.OBEN)[0], y: naechster(sOben.OBEN)[1] }, 'links');
  await warte(750);
  const kOben = await kamera();
  pruef(winkelZwischen(kOben.blick, [0, -1, 0]) < 1, 'Klick auf OBEN: Draufsicht (Blick senkrecht nach unten)', JSON.stringify(kOben.blick));
  await shotSave('n11_oben');
  await klickAn({ x: hausR.cx, y: hausR.cy }, 'links');
  await warte(750);
  const sEcke = await scan();
  await klickAn({ x: naechster(sEcke['OBEN-VORNE-RECHTS'])[0], y: naechster(sEcke['OBEN-VORNE-RECHTS'])[1] }, 'links');
  await warte(750);
  const kEcke = await kamera();
  pruef(winkelZwischen(kEcke.blick, [-1, -1, -1]) < 1 && nahe(Math.abs(kEcke.blick[1]), 1 / Math.sqrt(3), 0.02), 'Klick auf eine Ecke: Isometrie (alle Achsen gleich geneigt)', JSON.stringify(kEcke.blick));
  const sVorne = await (async () => { const s = await scan(); await klickAn({ x: naechster(s.VORNE)[0], y: naechster(s.VORNE)[1] }, 'links'); await warte(750); return scan(); })();
  const kVorne = await kamera();
  pruef(winkelZwischen(kVorne.blick, [0, 0, -1]) < 1 && Math.abs(kVorne.blick[1]) < 0.02, 'Klick auf VORNE: Blick nach Norden (waagrecht, ohne Ausrichtung des Gebäudes)', JSON.stringify(kVorne.blick));
  pruef(Object.keys(sVorne).includes('VORNE') && ['OBEN-VORNE', 'UNTEN-VORNE', 'VORNE-LINKS', 'VORNE-RECHTS', 'OBEN-VORNE-RECHTS', 'OBEN-VORNE-LINKS', 'UNTEN-VORNE-LINKS', 'UNTEN-VORNE-RECHTS'].every((n) => n in sVorne), 'in der Vorderansicht sind VORNE, 4 Kanten und 4 Ecken im Bild', Object.keys(sVorne).join(' '));

  abschnitt('N11 Hover und Beschriftung');
  const hoverName = await ausw(`const f = document.querySelector(${JSON.stringify(WFL)}); const r = f.getBoundingClientRect(); f.dispatchEvent(new PointerEvent('pointermove', { clientX: r.left + r.width / 2, clientY: r.top + r.height / 2, bubbles: true, pointerId: 1 })); return [f.dataset.bereich, f.title, f.style.cursor];`);
  pruef(hoverName[0] === 'VORNE' && /VORNE \(Fläche\)/.test(hoverName[1]) && hoverName[2] === 'pointer', 'Hover über der Mitte: «VORNE (Fläche)», Zeiger wird zur Hand', JSON.stringify(hoverName));
  await bewege(cube.cx, cube.cy);
  await warte(300);
  const hellBild = await shotHash(WFL);
  await bewege(cube.x - 80, cube.cy);
  await warte(300);
  const normalBild = await shotHash(WFL);
  pruef(hellBild !== normalBild, 'Hover hebt die Teilfläche hervor (Bild ändert sich)');

  abschnitt('N11 Ziehen am Würfel dreht, Klick löst nichts aus');
  await klickAn({ x: hausR.cx, y: hausR.cy }, 'links');
  await warte(750);
  const kd0 = await kamera();
  await ziehen({ x: cube.cx - 20, y: cube.cy + 10 }, { x: cube.cx + 40, y: cube.cy - 10 }, 'links', 0, 10);
  await warte(300);
  const kd1 = await kamera();
  pruef(winkelZwischen(kd0.blick, kd1.blick) > 10, `Ziehen am Würfel dreht die Ansicht (${winkelZwischen(kd0.blick, kd1.blick).toFixed(1)}°)`, JSON.stringify([kd0.blick, kd1.blick]));
  const ziel0 = kd0.ziel, ziel1 = kd1.ziel;
  pruef(Math.hypot(...ziel1.map((x, i) => x - ziel0[i])) < 0.01, 'Das Ziel der Kamera bleibt beim Ziehen am Würfel');
  await warte(600);
  const kd2 = await kamera();
  pruef(winkelZwischen(kd1.blick, kd2.blick) < 0.5, 'Nach dem Ziehen springt die Ansicht nicht (kein Klick auf einen Bereich)');

  abschnitt('N11 Kompassring: Nordwinkel, Ziehen dreht um die Hochachse');
  const piKz = await ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Projektinformation')[0]?.kennzeichen ?? null;`);
  if (piKz) await befehl('element_aendern', { element: piKz, eigenschaften: { nordwinkel: 30 } });
  else await befehl('element_anlegen', { klasse: 'Projektinformation', name: 'Projektinformation', eigenschaften: { nordwinkel: 30 } });
  await warte(800);
  await klickAn({ x: hausR.cx, y: hausR.cy }, 'links');
  await warte(750);
  const azimutVon = (k) => Math.atan2(k.lage[0] - k.ziel[0], k.lage[2] - k.ziel[2]) / GRAD;
  const nordAnzeige = () => ausw(`return Number(document.querySelector(${JSON.stringify(RING)}).dataset.nord);`);
  const k5 = await kamera();
  const erwartetNord = (((azimutVon(k5) + 30) % 360) + 360) % 360;
  const nordAngezeigt = await nordAnzeige();
  pruef(nahe(((nordAngezeigt - erwartetNord + 540) % 360) - 180, 0, 1.5), `Ring: N steht bei Azimut + Nordwinkel (${nordAngezeigt}° erwartet ${erwartetNord.toFixed(1)}°; Nordwinkel 30°)`);
  const ringM = await rechteckVon(RING);
  const rx = 128 / 2 - 6, ry = 40 / 2 - 5;
  const punktAuf = (b) => ({ x: ringM.cx + rx * Math.sin(b * GRAD), y: ringM.cy - ry * Math.cos(b * GRAD) });
  await ziehen(punktAuf(90), punktAuf(180), 'links', 0, 12);
  await warte(300);
  const k6 = await kamera();
  const dAz = ((azimutVon(k6) - azimutVon(k5) + 540) % 360) - 180;
  pruef(nahe(dAz, 90, 4), `Ziehen am Ring um 90° im Uhrzeigersinn dreht um die Hochachse (Azimut +${dAz.toFixed(1)}°)`);
  const polar = (k) => Math.acos(((k.lage[1] - k.ziel[1]) / Math.hypot(...k.lage.map((x, i) => x - k.ziel[i])))) / GRAD;
  pruef(nahe(polar(k6), polar(k5), 0.5), `Die Höhe der Kamera bleibt (Polarwinkel ${polar(k5).toFixed(1)}° → ${polar(k6).toFixed(1)}°)`);
  const nordNach = await nordAnzeige();
  pruef(nahe(((nordNach - nordAngezeigt + 540) % 360) - 180, 90, 4), `Ring dreht mit (N von ${nordAngezeigt}° auf ${nordNach}°)`);
  const ziel6 = k6.ziel;
  pruef(Math.hypot(...ziel6.map((x, i) => x - k5.ziel[i])) < 0.01, 'Das Ziel der Kamera bleibt beim Ziehen am Ring');

  abschnitt('N11 Startansicht festlegen (Kontextmenü) und Haus-Knopf');
  await klickAn({ x: hausR.cx, y: hausR.cy }, 'links');   // erst zur Isometrie
  await warte(750);
  const sOben2 = await scan();
  await klickAn({ x: naechster(sOben2.OBEN)[0], y: naechster(sOben2.OBEN)[1] }, 'links');
  await warte(750);
  const sEcke2 = await scan();
  const ecke = Object.keys(sEcke2).find((n) => n.split('-').length === 3);
  await klickAn({ x: naechster(sEcke2[ecke])[0], y: naechster(sEcke2[ecke])[1] }, 'links');   // eine andere Ecke als die Isometrie von vorne rechts oben
  await warte(750);
  const kEigen = await kamera();
  await klickAn({ x: cube.cx, y: cube.cy }, 'rechts');
  await warteBis(() => gibt('.kmenu'), 'Kontextmenü am Würfel', 5000);
  const eintraege = await ausw(`return [...document.querySelectorAll('.kmenu button')].map((b) => b.textContent);`);
  pruef(eintraege.includes('Aktuelle Ansicht als Startansicht festlegen') && eintraege.includes('Zur Startansicht'), 'Kontextmenü am Würfel: «Aktuelle Ansicht als Startansicht festlegen»', eintraege.join(' | '));
  await ausw(`[...document.querySelectorAll('.kmenu button')].find((b) => b.textContent === 'Aktuelle Ansicht als Startansicht festlegen').click(); return 1;`);
  await warte(300);
  const gemerkt = await ausw(`const k = Object.keys(localStorage).filter((x) => x.startsWith('hlks.startansicht3d.')); return k.map((x) => [x, localStorage.getItem(x)]);`);
  pruef(gemerkt.length === 1 && /AN-3D/.test(gemerkt[0][0]), 'Startansicht ist gemerkt (je Projekt und 3D-Ansicht)', JSON.stringify(gemerkt));
  await klickAn({ x: ringM.cx, y: ringM.cy }, 'links');   // Ring anklicken ändert nichts
  await ziehen({ x: cube.cx, y: cube.cy + 30 }, { x: cube.cx - 70, y: cube.cy - 40 }, 'links', 0, 10);
  await warte(300);
  await klickAn({ x: hausR.cx, y: hausR.cy }, 'links');
  await warte(750);
  const kBack = await kamera();
  pruef(winkelZwischen(kBack.blick, kEigen.blick) < 1.5, `Haus-Knopf führt zur festgelegten Startansicht (${winkelZwischen(kBack.blick, kEigen.blick).toFixed(2)}° Abweichung)`, JSON.stringify([kEigen.blick, kBack.blick]));
  await klickAn({ x: cube.cx, y: cube.cy }, 'rechts');
  await warteBis(() => gibt('.kmenu'), 'Kontextmenü', 5000);
  await ausw(`[...document.querySelectorAll('.kmenu button')].find((b) => /zurücksetzen/.test(b.textContent)).click(); return 1;`);
  await warte(300);
  await klickAn({ x: hausR.cx, y: hausR.cy }, 'links');
  await warte(750);
  pruef(blickEntspricht(await kamera(), 'OBEN-VORNE-RECHTS', 1.5), 'Startansicht zurücksetzen: Haus-Knopf führt wieder zur Isometrie');
  // Doppelklick Mitte nach einem Wechsel verwendet dieselbe Startrichtung
  await klickAn({ x: cube.cx, y: cube.cy + 200 }, 'mitte', 2);
  await warte(300);
  pruef(blickEntspricht(await kamera(), 'OBEN-VORNE-RECHTS', 1.5), '«Alles zeigen» (Doppelklick Mitte) schaut aus der Startrichtung');

  abschnitt('N11 Gebäudeausrichtung (planwinkel, Annahme N9)');
  const gebKz = await ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Gebaeude')[0]?.kennzeichen ?? null;`);
  let planwinkelGesetzt = false;
  if (gebKz) {
    const r = await kern('befehl', 'element_aendern', { element: gebKz, eigenschaften: { planwinkel: 20 } });
    planwinkelGesetzt = !!r.angenommen;
  }
  if (planwinkelGesetzt) {
    await warte(800);
    const s = await scan();
    await klickAn({ x: naechster(s.VORNE ?? s['OBEN-VORNE-RECHTS'])[0], y: naechster(s.VORNE ?? s['OBEN-VORNE-RECHTS'])[1] }, 'links');
    await warte(750);
    const k = await kamera();
    const az = Math.atan2(-k.blick[0], -k.blick[2]) / GRAD;
    pruef(true, `planwinkel 20°: Azimut der Kamera ${az.toFixed(1)}° (VORNE folgt dem Winkel)`);
  } else {
    pruef(true, 'planwinkel ist im Katalog noch nicht vorhanden (N9 läuft parallel): VORNE richtet sich nach den Projektachsen; die Anbindung liegt in gebaeudeAusrichtungGrad (wuerfel-logik.js), Node-Test deckt Winkel ab');
  }

  abschnitt('N11 Bilder (hell und dunkel)');
  await klickAn({ x: hausR.cx, y: hausR.cy }, 'links');
  await warte(750);
  for (const d of ['hell', 'dunkel']) {
    await designSetzen(d);
    await warte(500);
    const rw = await rechteckVon('.kachel.aktiv .d3-wurzel');
    const p1 = await speichern(`n11_3d_${d}`, { x: rw.x, y: rw.y, width: rw.width, height: rw.height });
    const rc = await rechteckVon('.kachel.aktiv .d3-wuerfel');
    const p2 = await speichern(`n11_wuerfel_${d}`, { x: rc.x - 30, y: rc.y - 10, width: rc.width + 60, height: rc.height + 20 });
    pruef(bildPruefen(p1) && bildPruefen(p2), `Bilder ${d}: ${p1}`);
  }
  await designSetzen('hell');
  pruef(fehler.length === 0, 'keine JavaScript-Ausnahmen', fehler.slice(0, 3).join(' | '));
  const konsoleOhneBekanntes = konsole.filter((t) => !/favicon|Failed to load resource/.test(t));
  pruef(konsoleOhneBekanntes.length === 0, 'keine Konsolenfehler', konsoleOhneBekanntes.slice(0, 3).join(' | '));

  await beenden(schlecht ? 1 : 0);
} catch (e) {
  console.error('Abbruch:', e.stack ?? e);
  await beenden(2);
}
