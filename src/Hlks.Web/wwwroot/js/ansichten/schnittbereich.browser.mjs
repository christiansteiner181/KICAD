// Browser-Integrationstest der Nachbearbeitung N13 (3D-Schnittbereich), N14 («Raum im 3D zeigen») und des Rests von N11 (Startansicht im Modell):
// Edge headless über CDP, echte Mausereignisse, frisches Beispielprojekt «MFH Musterweg». Nicht Teil von «node --test». Aufruf:
//   node schnittbereich.browser.mjs        (Port 5480, CDP 9480; HLKS_N13_PORT, HLKS_CDP_PORT; HLKS_BUILD wie beim Bauen)
// Der Server läuft mit HLKS_TEST=1 und allen Ablageorten unter %TEMP%\HLKS_CAD\n13_test – nie auf W:. Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT).
//
// Prüft:
//  N13  Schalter in den Eigenschaften der 3D-Ansicht schaltet den Bereich ein (Quader, sechs Griffe, Clipping an allen Materialien), er wird im Modell gespeichert;
//       Ziehen eines Griffs (echte Maus) verschiebt die Fläche und speichert (höchstens ein bis zwei Pakete); ein genau gesetzter Bereich schneidet alles über dem
//       Erdgeschoss weg, und dort ist nichts mehr wählbar (Treffer nur im Sichtbaren); Ausschalten gibt alles frei; Menüband-Knöpfe «An Auswahl anpassen»
//       (Auswahl + 300 mm), «Auf Geschoss» (Menü, OK/UK), «Zurücksetzen» (ganzes Modell); Navigationswürfel und Haus-Knopf mit eingeschaltetem Bereich; nach dem
//       Neuladen steht alles wieder da.
//  N14  Rechtsklick auf einen Raum im Grundriss → «Raum im 3D zeigen»: 3D-Ansicht, Schnittbereich = Raum + 1,5 m (Umriss, Höhe), Kamera schräg von vorne rechts, alle
//       Griffe im Bild; mehrere Räume; ohne 3D-Ansicht wird eine angelegt; Rand als Benutzereinstellung.
//  N11  Startansicht steht als Eigenschaft startansicht im Modell und bleibt nach dem Neuladen; eine alte Startansicht im Browser wird einmal übernommen.
// Bilder: %TEMP%\HLKS_CAD\n13\*.png (SHOTS ändert den Ordner).
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_N13_PORT ?? 5480);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9480);
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'n13');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'n13_test');
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
for (const d of [ABLAGE, path.join(WURZEL, 'bib'), path.join(WURZEL, 'aus'), path.join(WURZEL, 'zuletzt'), path.join(WURZEL, 'benutzer')]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_N13_Weg 1', 'c_Pläne und Modelle', 'e_KICAD');
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
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'N13', '--geraet', 'n13-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const kern = async (art, name, body) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const befehl = async (name, body) => { const r = await kern('befehl', name, body); if (!r.angenommen) throw new Error(`${name}: ${r.fehler ?? r.meldung}`); return r; };

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgen13-'));
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
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };
const gehe = async () => {
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { if (!localStorage.getItem('hlks.design')) localStorage.setItem('hlks.design', JSON.stringify('hell')); } catch (e) {}` }).catch(() => {});
  await send('Page.navigate', { url: `${SERVER}/` });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 30000);
  await warte(700);
};

// ------------------------------------------------------------------ Mausereignisse
const BTN = { links: ['left', 1], mitte: ['middle', 4], rechts: ['right', 2] };
const bewege = (x, y, buttons = 0, modifiers = 0) => send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y, buttons, modifiers });
async function ziehen(von, nach, taste = 'links', modifiers = 0, schritte = 8) {
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
const rechteckVon = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left, y: r.top, width: r.width, height: r.height, cx: r.left + r.width / 2, cy: r.top + r.height / 2 };`);
const gibt = async (sel) => (await ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`)) > 0;
const norm = (v) => { const l = Math.hypot(...v) || 1; return v.map((x) => x / l); };
const winkelZwischen = (a, b) => Math.acos(Math.max(-1, Math.min(1, norm(a).reduce((s, x, i) => s + x * norm(b)[i], 0)))) / GRAD;

// ------------------------------------------------------------------ Hilfen für 3D, Modell und Menüband
const AKTIV3D = '.kachel.aktiv .d3-wurzel';
const kamera = () => ausw(`const d = document.querySelector('${AKTIV3D}')?.dataset.kamera; return d ? JSON.parse(d) : null;`);
const sbDaten = () => ausw(`const d = document.querySelector('${AKTIV3D}')?.dataset.schnittbereich; return d && d !== 'null' ? JSON.parse(d) : null;`);
const clipping = () => ausw(`const d = document.querySelector('${AKTIV3D}')?.dataset.clipping; return d ? JSON.parse(d) : null;`);
const griffeLage = () => ausw(`const d = document.querySelector('${AKTIV3D}')?.dataset.griffe; return d ? JSON.parse(d) : null;`);
const sbModell = (kz = 'AN-3D') => ausw(`const z = await import('/js/zustand.js'); const a = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === ${JSON.stringify(kz)}); return a?.werte?.schnittbereich ?? null;`);
const modellWert = (kz, name) => ausw(`const z = await import('/js/zustand.js'); const a = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === ${JSON.stringify(kz)}); return a?.werte?.[${JSON.stringify(name)}] ?? null;`);
const pakete = () => ausw(`const z = await import('/js/zustand.js'); const p = z.zustand.stand?.pakete; return Array.isArray(p) ? p.length : Number(p);`);
const oeffnen = async (kz) => { const r = await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === ${JSON.stringify(kz)}); if (!e) return 'fehlt'; await f.ansichtOeffnen(e.id); return 'ok';`); if (r !== 'ok') throw new Error('Ansicht fehlt: ' + kz); await warte(900); };
const schliesseAlle = async () => { await ausw(`document.querySelectorAll('.kachel .zu').forEach((b) => b.click()); return 1;`); await warte(400); };
const zeige = async (kz) => { await schliesseAlle(); await oeffnen(kz); };
const dreiDGeladen = () => warteBis(() => ausw(`const w = document.querySelector('${AKTIV3D}'); return !!w?.dataset.kamera && w.querySelector('.d3-hinweis')?.hidden === true;`), '3D geladen', 30000).then(() => warte(600));
const inst = (kz, ausdruck) => ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === ${JSON.stringify(kz)}); const i = f.alle().find((x) => x.id === e.id)?.instanz; return (${ausdruck});`);
const register = async (name) => { await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === ${JSON.stringify(name)}).click(); return true;`); await warte(300); };
async function knopf(id) {
  const p = await ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return null; const r = k.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
  if (!p) throw new Error('Knopf fehlt: ' + id);
  await klickAn(p, 'links'); await warte(500);
}
const knopfInfo = (id) => ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return null; return { gesperrt: k.classList.contains('gesperrt'), gedrueckt: k.classList.contains('gedrueckt'), gruppe: k.closest('.mb-gruppe')?.querySelector('.mb-gruppenname')?.textContent ?? '', symbol: !!k.querySelector('svg path, svg rect, svg circle') };`);
const auswahlSetzen = (id) => ausw(`const z = await import('/js/zustand.js'); z.auswaehlen(${JSON.stringify(id)}); return 1;`).then(() => warte(400));
/** Raster über die Leinwand: Treffer je Element (Anzahl) – was an dieser Stelle gewählt würde. */
const raster = (kz, nx = 40, ny = 24) => inst(kz, `(() => { const c = document.querySelector('${AKTIV3D} .d3-leinwand').getBoundingClientRect(); const aus = {}; for (let j = 0; j < ${ny}; j++) for (let k = 0; k < ${nx}; k++) { const id = i.trefferBei(c.left + (k + 0.5) * c.width / ${nx}, c.top + (j + 0.5) * c.height / ${ny}); if (id) aus[id] = (aus[id] ?? 0) + 1; } return aus; })()`);
const elementInfo = (ids) => ausw(`const z = await import('/js/zustand.js'); return ${JSON.stringify(ids)}.map((id) => { const e = z.zustand.elemente.get(id); return e ? { id, kz: e.kennzeichen, klasse: e.klasse, geschoss: e.geschoss, z0: e.geometrie?.punkte?.[0]?.[2] ?? null } : { id }; });`);

// ------------------------------------------------------------------ Modell (aus der Seite)
try {
  await gehe();
  const info = await ausw(`const z = await import('/js/zustand.js');
    const gs = z.derKlasse('Geschoss').map((g) => ({ kz: g.kennzeichen, id: g.id, name: g.name, kote: g.werte.kote, hoehe: g.werte.hoehe })).sort((a, b) => a.kote - b.kote);
    const rs = z.derKlasse('Raum').map((r) => ({ kz: r.kennzeichen, id: r.id, geschoss: r.geschoss, punkte: r.geometrie?.punkte ?? [], lh: r.werte?.lichteHoehe ?? null }));
    const geb = z.derKlasse('Gebaeude')[0];
    return { gs, rs, planwinkel: geb?.werte?.planwinkel ?? null };`);
  const winkelErwartet = Number.isFinite(info.planwinkel) ? (info.planwinkel === 0 ? 0 : -info.planwinkel) : null;
  pruef(info.gs.length >= 2 && info.rs.length >= 2, `Beispielprojekt: ${info.gs.length} Geschosse, ${info.rs.length} Räume, Planwinkel ${info.planwinkel}`);
  const eg = info.gs.find((g) => /^EG/i.test(g.kz) || /Erd/i.test(g.name ?? '')) ?? info.gs[1];
  const hoeher = info.gs.filter((g) => g.kote > eg.kote);
  const raeumeEG = info.rs.filter((r) => r.geschoss === eg.kz);

  /** Erwarteter Schnittbereich zu Räumen (unabhängig vom Kern gerechnet): Umriss in den Achsen des Winkels plus Rand, Höhe Unterkante − Rand … Oberkante + Rand. */
  const erwartet = (raeume, rand, winkel) => {
    const t = winkel * GRAD, c = Math.cos(t), s = Math.sin(t);
    let u0 = Infinity, u1 = -Infinity, v0 = Infinity, v1 = -Infinity, z0 = Infinity, z1 = -Infinity;
    for (const r of raeume) {
      const g = info.gs.find((x) => x.kz === r.geschoss);
      const uk = Math.min(...r.punkte.map((p) => (p.length > 2 ? p[2] : g.kote)));
      z0 = Math.min(z0, uk); z1 = Math.max(z1, uk + (r.lh ?? g.hoehe));
      for (const p of r.punkte) { const u = p[0] * c + p[1] * s, v = -p[0] * s + p[1] * c; u0 = Math.min(u0, u); u1 = Math.max(u1, u); v0 = Math.min(v0, v); v1 = Math.max(v1, v); }
    }
    return { min: [u0 - rand, v0 - rand, z0 - rand], max: [u1 + rand, v1 + rand, z1 + rand] };
  };
  const gleichBox = (a, b, eps = 1) => [0, 1, 2].every((i) => nahe(a.min[i], b.min[i], eps) && nahe(a.max[i], b.max[i], eps));

  // ================================================================== N13: Einschalten über die Eigenschaften
  abschnitt('N13 Schalter in den Eigenschaften der 3D-Ansicht');
  await zeige('AN-3D');
  await dreiDGeladen();
  await auswahlSetzen(null);
  await warteBis(() => gibt('.ep-sb-schalter'), 'Schalter in der Palette', 8000);
  pruef(!(await sbModell()) && !(await sbDaten()), 'ohne Bereich: nichts gesetzt, keine Griffe');
  const roh0 = await raster('AN-3D');
  const ids0 = Object.keys(roh0);
  pruef(ids0.length >= 5, `Raster findet gewählte Körper (${ids0.length})`);
  const pak0 = await pakete();
  await ausw(`document.querySelector('.ep-sb-schalter').scrollIntoView({ block: 'center' }); return 1;`);   // die Palette ist schmal und rollt
  await warte(300);
  const sch = await rechteckVon('.ep-sb-schalter');
  await klickAn({ x: sch.cx, y: sch.cy }, 'links');
  await warteBis(async () => (await sbModell())?.aktiv === true, 'Bereich im Modell eingeschaltet', 8000);
  await warte(500);
  const sb1 = await sbDaten();
  const m1 = await sbModell();
  pruef(sb1?.aktiv === true && gleichBox(sb1, m1, 0.2), 'Bereich ist ein, Anzeige und Modell stimmen überein', JSON.stringify([sb1, m1]));
  pruef(winkelErwartet === null || nahe(sb1.winkel, winkelErwartet, 0.01), `Quader am Planwinkel ausgerichtet (Winkel ${sb1.winkel}°, erwartet ${winkelErwartet}°)`);
  pruef(await ausw(`return document.querySelector('.ep-sb-schalter').checked === true;`), 'Haken in der Palette gesetzt');
  pruef((await pakete()) - pak0 === 1, `ein Paket für das Einschalten (${(await pakete()) - pak0})`);
  const cl1 = await clipping();
  pruef(cl1 && cl1.materialien > 3 && cl1.geclippt === cl1.materialien, `alle Materialien haben die sechs Clipping-Ebenen (${cl1?.geclippt}/${cl1?.materialien})`, JSON.stringify(cl1));
  const lage1 = await griffeLage();
  const lein = await rechteckVon(`${AKTIV3D} .d3-leinwand`);
  pruef(lage1?.length === 6 && lage1.every((g) => g.vorn && g.x > 0 && g.y > 0 && g.x < lein.width && g.y < lein.height), 'sechs Griffe, alle im Bild', JSON.stringify(lage1));
  const rasterAn = await raster('AN-3D');
  const gemeinsam = Object.keys(rasterAn).filter((id) => ids0.includes(id)).length;
  pruef(gemeinsam >= ids0.length * 0.85, `das ganze Modell bleibt sichtbar und wählbar (${gemeinsam} von ${ids0.length})`);
  const bildAn = await speichern('n13-bereich-ganzes-modell', { x: lein.x, y: lein.y, width: lein.width, height: lein.height });
  pruef(bildPruefen(bildAn), 'Prüfbild: Bereich eingeschaltet, ganzes Modell');

  // ================================================================== N13: Griff ziehen
  abschnitt('N13 Griff ziehen (echte Maus): Fläche verschiebt sich, gespeichert wird einmal');
  const gOben = lage1.find((g) => g.name === 'z+');
  const von = { x: lein.x + gOben.x, y: lein.y + gOben.y };
  const pak1 = await pakete();
  const kVorZug = await kamera();
  await ziehen(von, { x: von.x, y: von.y + 110 }, 'links', 0, 12);
  await warte(300);
  const sb2 = await sbDaten();
  pruef(sb2.max[2] < sb1.max[2] - 200 && nahe(sb2.min[2], sb1.min[2], 0.2) && nahe(sb2.max[0], sb1.max[0], 0.2) && nahe(sb2.max[1], sb1.max[1], 0.2), `Dach des Bereichs ist tiefer (${sb1.max[2].toFixed(0)} → ${sb2.max[2].toFixed(0)} mm), alles andere bleibt`);
  await warteBis(async () => nahe((await sbModell())?.max?.[2], sb2.max[2], 0.2), 'neue Höhe im Modell', 8000);
  const dp = (await pakete()) - pak1;
  pruef(dp >= 1 && dp <= 2, `das Ziehen ergibt ein bis zwei Pakete (${dp})`);
  const kNachZug = await kamera();
  pruef(kNachZug && winkelZwischen(kNachZug.blick, kVorZug.blick) < 0.5, `Kamera blieb stehen (Ziehen am Griff dreht nicht, ${winkelZwischen(kNachZug.blick, kVorZug.blick).toFixed(2)}°)`);
  // seitlicher Griff nach aussen und wieder zurück
  const lage2 = await griffeLage();
  const gU = lage2.find((g) => g.name === 'u+');
  const vonU = { x: lein.x + gU.x, y: lein.y + gU.y };
  await ziehen(vonU, { x: vonU.x + 60, y: vonU.y - 25 }, 'links', 0, 10);
  await warte(300);
  const sb3 = await sbDaten();
  pruef(Math.abs(sb3.max[0] - sb2.max[0]) > 100 && nahe(sb3.min[0], sb2.min[0], 0.2) && nahe(sb3.max[2], sb2.max[2], 0.2), `Griff u+ verschiebt nur die Fläche u+ (${sb2.max[0].toFixed(0)} → ${sb3.max[0].toFixed(0)} mm)`);
  await warteBis(async () => nahe((await sbModell())?.max?.[0], sb3.max[0], 0.2), 'u+ im Modell', 8000);

  // ================================================================== N13: genau gesetzt, nur Sichtbares
  abschnitt('N13 Bereich auf das Erdgeschoss: oberhalb ist nichts mehr sichtbar oder wählbar');
  const winkel = sb3.winkel;
  const ganz = await sbModell();
  const nurEG = { aktiv: true, min: [ganz.min[0] - 2000, ganz.min[1] - 2000, eg.kote - 300], max: [ganz.max[0] + 2000, ganz.max[1] + 2000, eg.kote + eg.hoehe - 50], winkel };
  await befehl('element_aendern', { element: 'AN-3D', eigenschaften: { schnittbereich: nurEG } });
  await warteBis(async () => nahe((await sbDaten())?.max?.[2], nurEG.max[2], 0.2), 'Bereich auf EG gesetzt', 8000);
  await ausw(`document.querySelector('${AKTIV3D}').closest('.kachel').querySelector('.d3-leinwand').dispatchEvent(new PointerEvent('pointermove', { bubbles: true })); return 1;`);
  await inst('AN-3D', 'i.zoomAlles()');
  await warte(500);
  const rasterEG = await raster('AN-3D');
  const infoAlt = await elementInfo(Object.keys(roh0));
  const infoNeu = await elementInfo(Object.keys(rasterEG));
  const oben0 = infoAlt.filter((e) => e.z0 !== null && e.z0 >= eg.kote + eg.hoehe - 10);
  const oben1 = infoNeu.filter((e) => e.z0 !== null && e.z0 >= eg.kote + eg.hoehe - 10);
  pruef(oben0.length > 0, `vorher waren Teile über dem EG wählbar (${oben0.length}: ${oben0.slice(0, 4).map((e) => e.kz).join(', ')})`);
  pruef(oben1.length === 0, `jetzt ist kein Teil über dem EG mehr wählbar (${oben1.map((e) => e.kz).join(', ') || 'keins'})`);
  pruef(infoNeu.some((e) => e.geschoss === eg.kz), 'Teile des EG bleiben wählbar');
  const cl2 = await clipping();
  pruef(cl2.geclippt === cl2.materialien, 'Clipping weiterhin an allen Materialien');
  // Haus-Knopf und Würfel mit eingeschaltetem Bereich: der Bereich ist im Bild
  const hausR = await rechteckVon(`${AKTIV3D} .d3-haus`);
  const wuerfelR = await rechteckVon(`${AKTIV3D} .d3-wuerfel-flaeche`);
  await klickAn({ x: hausR.cx, y: hausR.cy }, 'links');
  await warte(750);
  const lageEG = await griffeLage();
  pruef(lageEG.every((g) => g.vorn && g.x > 0 && g.y > 0 && g.x < lein.width && g.y < lein.height), 'Haus-Knopf stellt den Bereich ganz ins Bild (alle Griffe sichtbar)', JSON.stringify(lageEG));
  const bildEG = await speichern('n13-bereich-erdgeschoss', { x: lein.x, y: lein.y, width: lein.width, height: lein.height });
  pruef(bildPruefen(bildEG), 'Prüfbild: Bereich auf dem Erdgeschoss');
  // Würfel: Klick auf eine Fläche dreht weiter (Navigationswürfel bleibt nutzbar)
  const kv = await kamera();
  await ziehen({ x: wuerfelR.cx, y: wuerfelR.cy }, { x: wuerfelR.cx - 50, y: wuerfelR.cy + 10 }, 'links', 0, 8);
  const kn = await kamera();
  pruef(winkelZwischen(kv.blick, kn.blick) > 5, `Navigationswürfel dreht mit eingeschaltetem Bereich (${winkelZwischen(kv.blick, kn.blick).toFixed(1)}°)`);
  // Ausschalten gibt alles frei
  await register('Ansicht');
  const kSch = await knopfInfo('ansicht.schnittbereich');
  pruef(kSch && kSch.gruppe === '3D-Schnitt' && kSch.gedrueckt && !kSch.gesperrt && kSch.symbol, 'Menüband Ansicht › 3D-Schnitt: Knopf gedrückt, mit Symbol', JSON.stringify(kSch));
  for (const id of ['ansicht.schnittbereichAuswahl', 'ansicht.schnittbereichGeschoss', 'ansicht.schnittbereichZurueck', 'ansicht.raum3d']) {
    const k = await knopfInfo(id);
    pruef(k && k.gruppe === '3D-Schnitt' && k.symbol, `Menüband: Knopf ${id}`, JSON.stringify(k));
  }
  await knopf('ansicht.schnittbereich');
  await warteBis(async () => (await sbModell())?.aktiv === false, 'ausgeschaltet im Modell', 8000);
  await warte(400);
  const clAus = await clipping();
  pruef(clAus.geclippt === 0 && !(await sbDaten()), 'ausgeschaltet: keine Clipping-Ebenen, kein Quader, keine Griffe');
  await inst('AN-3D', 'i.zoomAlles()');
  await warte(400);
  const rasterAus = await raster('AN-3D');
  pruef(Object.keys(rasterAus).length >= ids0.length * 0.85, `alles wieder wählbar (${Object.keys(rasterAus).length})`);
  const mAus = await sbModell();
  pruef(gleichBox(mAus, nurEG, 0.2), 'der Quader bleibt im Modell erhalten (nur aktiv=false)');

  // ================================================================== N13: Auswahl, Geschoss, Zurücksetzen
  abschnitt('N13 «An Auswahl anpassen» (+300 mm), «Auf Geschoss», «Zurücksetzen»');
  const waende = await ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Wand').filter((w) => w.geschoss === ${JSON.stringify(eg.kz)}).map((w) => ({ id: w.id, kz: w.kennzeichen }));`);
  const wand = waende[0];
  await auswahlSetzen(wand.id);
  await register('Ansicht');
  const kA = await knopfInfo('ansicht.schnittbereichAuswahl');
  pruef(kA && !kA.gesperrt, 'Knopf «An Auswahl anpassen» frei bei gewähltem Element', JSON.stringify(kA));
  await knopf('ansicht.schnittbereichAuswahl');
  await warteBis(async () => { const m = await sbModell(); return m?.aktiv === true && m.max[2] - m.min[2] < ganz.max[2] - ganz.min[2] - 500; }, 'Bereich an der Auswahl', 8000);
  const sbW = await sbModell();
  const koerper = await (await fetch(`${SERVER}/api/koerper?ids=${wand.id}`)).json();
  const teile = (koerper[0]?.teile ?? []).filter((t) => t.art === 'prisma' || t.art === 'quader');
  const t = sbW.winkel * GRAD, c = Math.cos(t), s = Math.sin(t);
  let u0 = Infinity, u1 = -Infinity, v0 = Infinity, v1 = -Infinity, z0 = Infinity, z1 = -Infinity;
  for (const tl of teile) {   // Wände sind Quader (Mitte, Masse, Drehung um z) oder Prismen (Grundriss, unten, oben)
    let pts = [];
    if (tl.art === 'prisma') { pts = tl.grundriss.map((p) => [p[0], p[1]]); z0 = Math.min(z0, tl.unten); z1 = Math.max(z1, tl.oben); }
    else {
      const d = (tl.drehung ?? 0) * GRAD, [lx, ly, lz] = tl.masse;
      for (const sx of [-1, 1]) for (const sy of [-1, 1]) { const ex = (sx * lx) / 2, ey = (sy * ly) / 2; pts.push([tl.mitte[0] + ex * Math.cos(d) - ey * Math.sin(d), tl.mitte[1] + ex * Math.sin(d) + ey * Math.cos(d)]); }
      z0 = Math.min(z0, tl.mitte[2] - lz / 2); z1 = Math.max(z1, tl.mitte[2] + lz / 2);
    }
    for (const p of pts) { const u = p[0] * c + p[1] * s, v = -p[0] * s + p[1] * c; u0 = Math.min(u0, u); u1 = Math.max(u1, u); v0 = Math.min(v0, v); v1 = Math.max(v1, v); }
  }
  pruef(teile.length > 0 && Number.isFinite(u0) && gleichBox(sbW, { min: [u0 - 300, v0 - 300, z0 - 300], max: [u1 + 300, v1 + 300, z1 + 300] }, 1.5),
    `Bereich = Wand ${wand.kz} plus 300 mm Rand in allen Richtungen (Höhe ${sbW.min[2].toFixed(0)} … ${sbW.max[2].toFixed(0)})`, JSON.stringify([sbW, { u0, u1, v0, v1, z0, z1 }]));
  await auswahlSetzen(null);
  // Auf Geschoss ohne Auswahl: Menü der Geschosse
  await knopf('ansicht.schnittbereichGeschoss');
  await warteBis(() => gibt('.kmenu'), 'Menü der Geschosse', 5000);
  const eintraege = await ausw(`return [...document.querySelectorAll('.kmenu button')].map((b) => b.textContent);`);
  pruef(eintraege.length === info.gs.length, `Menü nennt alle ${info.gs.length} Geschosse`, eintraege.join(' | '));
  const wahl = hoeher[0] ?? info.gs[info.gs.length - 1];
  await ausw(`[...document.querySelectorAll('.kmenu button')][${info.gs.findIndex((g) => g.kz === wahl.kz)}].click(); return 1;`);
  await warteBis(async () => nahe((await sbModell())?.min?.[2], wahl.kote, 0.2), 'Bereich auf Geschoss', 8000);
  const sbG = await sbModell();
  pruef(nahe(sbG.min[2], wahl.kote, 0.2) && nahe(sbG.max[2], wahl.kote + wahl.hoehe, 0.2), `Auf Geschoss ${wahl.kz}: OK ${wahl.kote} bis ${wahl.kote + wahl.hoehe} mm`, JSON.stringify(sbG));
  pruef(sbG.max[0] - sbG.min[0] > sbW.max[0] - sbW.min[0] || sbG.max[1] - sbG.min[1] > sbW.max[1] - sbW.min[1] - 1, 'der Grundriss des Bereichs bleibt (nur die Höhe wechselt)');
  // Zurücksetzen
  await knopf('ansicht.schnittbereichZurueck');
  await warteBis(async () => { const m = await sbModell(); return m && m.max[2] - m.min[2] > sbG.max[2] - sbG.min[2] + 500; }, 'zurückgesetzt', 8000);
  const sbZ = await sbModell();
  pruef(sbZ.aktiv === true && gleichBox(sbZ, ganz, Math.max(1, 0.002 * (ganz.max[0] - ganz.min[0]))) || (sbZ.aktiv === true && sbZ.max[0] - sbZ.min[0] >= (ganz.max[0] - ganz.min[0]) - 4500 && sbZ.min[2] <= info.gs[0].kote + 1),
    'Zurücksetzen: ganzes Modell', JSON.stringify([sbZ, ganz]));
  await warte(400);
  const rasterZ = await raster('AN-3D');
  pruef(Object.keys(rasterZ).length >= ids0.length * 0.85, `nach dem Zurücksetzen ist wieder alles wählbar (${Object.keys(rasterZ).length})`);

  // ================================================================== N14: Raum im 3D zeigen
  abschnitt('N14 Raum im Grundriss wählen, Kontextmenü «Raum im 3D zeigen»');
  const raum1 = raeumeEG[0];
  const raum2 = raeumeEG[1];
  await befehl('element_aendern', { element: 'AN-3D', eigenschaften: { schnittbereich: null } });
  await auswahlSetzen(null);
  await zeige('AN-GR-EG');
  await warteBis(() => ausw(`return !!document.querySelector('.kachel.aktiv svg.gr-svg [data-id]');`), 'Grundriss gezeichnet', 15000);
  await warte(600);
  const mitte = (r) => [r.punkte.reduce((a, p) => a + p[0], 0) / r.punkte.length, r.punkte.reduce((a, p) => a + p[1], 0) / r.punkte.length];
  const bild = await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-GR-EG'); const i = f.alle().find((x) => x.id === e.id).instanz; const p = i.zuBildschirm(${JSON.stringify(mitte(raum1))}); return p;`);
  await klickAn({ x: bild.x, y: bild.y }, 'rechts');
  await warteBis(() => gibt('.kmenu'), 'Kontextmenü im Grundriss', 5000);
  const menue = await ausw(`return [...document.querySelectorAll('.kmenu button')].map((b) => b.textContent);`);
  pruef(menue.includes('Raum in 3D'), 'Rechtsklick auf den Raum: Kontextmenü mit «Raum in 3D»', menue.join(' | '));
  pruef((await ausw(`const z = await import('/js/zustand.js'); return z.zustand.auswahl;`)) === raum1.id, `der Raum ${raum1.kz} ist dabei gewählt`);
  const pakN14 = await pakete();
  await ausw(`[...document.querySelectorAll('.kmenu button')].find((b) => b.textContent === 'Raum in 3D').click(); return 1;`);
  await warteBis(async () => (await sbDaten())?.aktiv === true, 'Schnittbereich auf dem Raum', 15000);
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return z.zustand.aktiveAnsicht === 'AN-3D';`), '3D-Ansicht aktiv', 8000);
  await warte(900);
  const sbR = await sbDaten();
  const erw = erwartet([raum1], 1500, sbR.winkel);
  pruef(gleichBox(sbR, erw, 1.5), 'Schnittbereich = Raum + 1500 mm (Umriss in den Achsen des Gebäudes, Höhe Unterkante − 1.5 m … Oberkante + 1.5 m)', JSON.stringify([sbR, erw]));
  pruef(winkelErwartet === null || nahe(sbR.winkel, winkelErwartet, 0.01), `Quader am Planwinkel ausgerichtet (${sbR.winkel}°)`);
  pruef(gleichBox(sbR, await sbModell(), 0.2), 'im Modell gespeichert');
  pruef((await pakete()) - pakN14 === 1, `der Befehl ergibt ein Paket (${(await pakete()) - pakN14})`);
  const theta = (winkelErwartet ?? sbR.winkel) * GRAD;
  const isoR = norm([Math.cos(theta) + Math.sin(theta), 1, -Math.sin(theta) + Math.cos(theta)]);
  const kR = await kamera();
  pruef(winkelZwischen(kR.blick, isoR.map((x) => -x)) < 2.5, `Kamera schräg von vorne rechts oben (${winkelZwischen(kR.blick, isoR.map((x) => -x)).toFixed(1)}° Abweichung zur Isometrie)`, JSON.stringify(kR.blick));
  const leinR = await rechteckVon(`${AKTIV3D} .d3-leinwand`);
  const lageR = await griffeLage();
  pruef(lageR?.length === 6 && lageR.every((g) => g.vorn && g.x > 5 && g.y > 5 && g.x < leinR.width - 5 && g.y < leinR.height - 5), 'der ganze Bereich ist im Bild (alle sechs Griffe sichtbar)', JSON.stringify(lageR));
  const rasterR = await raster('AN-3D');
  const infoR = await elementInfo(Object.keys(rasterR));
  pruef(Object.keys(rasterR).length >= 1 && infoR.every((e) => e.geschoss !== undefined), `im Bild sind nur Teile des Bereichs wählbar (${Object.keys(rasterR).length} Elemente)`);
  const bildRaum = await speichern('n14-raum-im-3d', { x: leinR.x, y: leinR.y, width: leinR.width, height: leinR.height });
  pruef(bildPruefen(bildRaum), 'Prüfbild: Raum im 3D gezeigt');
  await speichern('n14-raum-im-3d-gesamt');

  abschnitt('N14 mehrere Räume, Rand als Benutzereinstellung, ohne 3D-Ansicht');
  await ausw(`const r = await import('/js/oberflaeche/register.js'); await r.ausfuehren('ansicht.raum3d', { raeume: ${JSON.stringify([raum1.kz, raum2.kz])} }); return 1;`);
  await warteBis(async () => gleichBox(await sbDaten() ?? { min: [0, 0, 0], max: [0, 0, 0] }, erwartet([raum1, raum2], 1500, sbR.winkel), 1.5), 'gemeinsamer Bereich zweier Räume', 10000);
  pruef(true, `zwei Räume (${raum1.kz}, ${raum2.kz}) ergeben einen gemeinsamen Bereich`);
  const setzeRand = await fetch(`${SERVER}/api/benutzer/einstellungen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ schluessel: 'ansicht3d.raumrand', wert: 500 }) });
  pruef(setzeRand.ok, 'Benutzereinstellung ansicht3d.raumrand = 500 gespeichert', await setzeRand.text().catch(() => ''));
  await ausw(`const b = await import('/js/oberflaeche/benutzer.js'); await b.benutzerLaden(); return b.benutzerWert('ansicht3d.raumrand', null);`);
  await ausw(`const r = await import('/js/oberflaeche/register.js'); await r.ausfuehren('ansicht.raum3d', { raeume: ${JSON.stringify([raum1.kz])} }); return 1;`);
  await warteBis(async () => gleichBox(await sbDaten() ?? { min: [0, 0, 0], max: [0, 0, 0] }, erwartet([raum1], 500, sbR.winkel), 1.5), 'Rand 500 mm', 10000);
  pruef(true, 'Rand 500 mm aus der Benutzereinstellung wirkt');
  await fetch(`${SERVER}/api/benutzer/einstellungen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ schluessel: 'ansicht3d.raumrand', wert: null }) });
  await ausw(`const b = await import('/js/oberflaeche/benutzer.js'); await b.benutzerLaden(); return 1;`);
  // ohne 3D-Ansicht: der Befehl legt AN-3D an
  await schliesseAlle();
  await befehl('element_loeschen', { element: 'AN-3D' });
  await warteBis(async () => !(await modellWert('AN-3D', 'art')), 'AN-3D gelöscht', 5000);
  await zeige('AN-GR-EG');
  await warteBis(() => ausw(`return !!document.querySelector('.kachel.aktiv svg.gr-svg [data-id]');`), 'Grundriss gezeichnet', 15000);
  await warte(500);
  await auswahlSetzen(raum2.id);
  await ausw(`const r = await import('/js/oberflaeche/register.js'); await r.ausfuehren('ansicht.raum3d'); return 1;`);
  await warteBis(async () => (await modellWert('AN-3D', 'art')) === '3d' && (await sbModell())?.aktiv === true, 'AN-3D angelegt', 10000);
  await warteBis(async () => (await sbDaten())?.aktiv === true, 'neue 3D-Ansicht zeigt den Bereich', 15000);
  pruef(gleichBox(await sbModell(), erwartet([raum2], 1500, sbR.winkel), 1.5), 'ohne 3D-Ansicht: AN-3D angelegt, Bereich auf dem Raum + 1500 mm');
  await warte(900);
  const leinN = await rechteckVon(`${AKTIV3D} .d3-leinwand`);
  const lageN = await griffeLage();
  pruef(lageN?.length === 6 && lageN.every((g) => g.vorn && g.x > 5 && g.y > 5 && g.x < leinN.width - 5 && g.y < leinN.height - 5), 'die neue Ansicht zeigt den ganzen Bereich (Kamera nach dem Laden nicht überschrieben)', JSON.stringify(lageN));

  // ================================================================== N11 Rest: Startansicht im Modell
  abschnitt('N11 Startansicht im Modell und Übernahme der alten Browser-Startansicht');
  await befehl('element_aendern', { element: 'AN-3D', eigenschaften: { schnittbereich: null } });
  await schliesseAlle();
  // alte Startansicht im Browser, nichts im Modell
  const schluessel = await ausw(`const z = await import('/js/zustand.js'); const w = await import('/js/ansichten/wuerfel-logik.js'); return w.startansichtSchluessel(z.zustand.status?.ort ?? z.zustand.status?.projekt, 'AN-3D');`);
  await ausw(`localStorage.setItem('hlks.' + ${JSON.stringify(schluessel)}, JSON.stringify({ azimutGrad: 100, polarGrad: 70 })); return 1;`);
  pruef(!(await modellWert('AN-3D', 'startansicht')), 'vor dem Öffnen: im Modell keine Startansicht');
  await oeffnen('AN-3D');
  await dreiDGeladen();
  await warteBis(async () => (await modellWert('AN-3D', 'startansicht'))?.azimutGrad === 100, 'alte Startansicht ins Modell übernommen', 8000);
  const sm = await modellWert('AN-3D', 'startansicht');
  pruef(sm.azimutGrad === 100 && sm.polarGrad === 70 && sm.projektion === 'perspektive', 'alte Startansicht (100° / 70°) steht jetzt im Modell', JSON.stringify(sm));
  pruef((await ausw(`return localStorage.getItem('hlks.' + ${JSON.stringify(schluessel)});`)) === null, 'der Browser-Wert ist entfernt (einmalige Übernahme)');
  await klickAn({ x: (await rechteckVon(`${AKTIV3D} .d3-haus`)).cx, y: (await rechteckVon(`${AKTIV3D} .d3-haus`)).cy }, 'links');
  await warte(800);
  const kHaus = await kamera();
  const ziel100 = (() => { const az = (100 + (-(winkelErwartet ?? 0) * 0)) * GRAD; const po = 70 * GRAD; const th = (winkelErwartet ?? 0) * GRAD; const lx = Math.sin(po) * Math.sin(az), ly = Math.cos(po), lz = Math.sin(po) * Math.cos(az); return [lx * Math.cos(th) + lz * Math.sin(th), ly, -lx * Math.sin(th) + lz * Math.cos(th)]; })();
  pruef(winkelZwischen(kHaus.blick, ziel100.map((x) => -x)) < 3, `Haus-Knopf führt zur übernommenen Startansicht (${winkelZwischen(kHaus.blick, ziel100.map((x) => -x)).toFixed(1)}° Abweichung)`, JSON.stringify([kHaus.blick, ziel100]));
  // neue Startansicht über das Kontextmenü des Würfels, Neuladen
  const w = await rechteckVon(`${AKTIV3D} .d3-wuerfel-flaeche`);
  await ziehen({ x: w.cx, y: w.cy }, { x: w.cx + 60, y: w.cy - 30 }, 'links', 0, 8);
  const kEigen = await kamera();
  await klickAn({ x: w.cx, y: w.cy }, 'rechts');
  await warteBis(() => gibt('.kmenu'), 'Kontextmenü am Würfel', 5000);
  await ausw(`[...document.querySelectorAll('.kmenu button')].find((b) => b.textContent === 'Aktuelle Ansicht als Startansicht festlegen').click(); return 1;`);
  await warteBis(async () => { const s = await modellWert('AN-3D', 'startansicht'); return s && s.azimutGrad !== 100; }, 'neue Startansicht im Modell', 8000);
  const s2 = await modellWert('AN-3D', 'startansicht');
  pruef(Array.isArray(s2.position) && Array.isArray(s2.ziel) && s2.position.every(Number.isFinite), `gespeichert mit Position und Ziel der Kamera: ${JSON.stringify(s2)}`);
  await gehe();
  await oeffnen('AN-3D');
  await dreiDGeladen();
  pruef(JSON.stringify(await modellWert('AN-3D', 'startansicht')) === JSON.stringify(s2), 'nach dem Neuladen steht die Startansicht noch im Modell');
  const hausN = await rechteckVon(`${AKTIV3D} .d3-haus`);
  await klickAn({ x: hausN.cx, y: hausN.cy }, 'links');
  await warte(800);
  const kNeu = await kamera();
  pruef(winkelZwischen(kNeu.blick, kEigen.blick) < 3, `Haus-Knopf führt nach dem Neuladen zur gespeicherten Startansicht (${winkelZwischen(kNeu.blick, kEigen.blick).toFixed(1)}°)`);

  // ================================================================== Neuladen mit eingeschaltetem Bereich
  abschnitt('N13 Neuladen: der Bereich bleibt mit der Ansicht gespeichert');
  await befehl('element_aendern', { element: 'AN-3D', eigenschaften: { schnittbereich: { aktiv: true, min: [ganz.min[0], ganz.min[1], eg.kote - 100], max: [ganz.max[0], ganz.max[1], eg.kote + eg.hoehe], winkel } } });
  await warte(600);
  await gehe();
  await oeffnen('AN-3D');
  await dreiDGeladen();
  const sbN = await sbDaten();
  pruef(sbN?.aktiv === true && nahe(sbN.max[2], eg.kote + eg.hoehe, 0.2), 'nach dem Neuladen ist der Bereich eingeschaltet und gleich', JSON.stringify(sbN));
  const clN = await clipping();
  pruef(clN?.geclippt > 0 && clN.geclippt === clN.materialien, 'und die Körper sind wieder abgeschnitten');

  pruef(fehler.length === 0, 'keine Skriptfehler in der Seite', fehler.join(' | ').slice(0, 400));
  const wichtig = konsole.filter((m) => !/favicon|404|ResizeObserver|WebGL|GPU stall|swiftshader/i.test(m));
  pruef(wichtig.length === 0, 'keine Fehler in der Konsole', wichtig.join(' | ').slice(0, 400));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e?.stack ?? e);
  try { await speichern('n13-abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} ok, ${schlecht} fehlgeschlagen`);
await beenden(schlecht > 0 ? 1 : 0);
