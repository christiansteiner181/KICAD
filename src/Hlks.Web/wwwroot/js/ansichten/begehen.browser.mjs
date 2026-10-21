// Browser-Integrationstest der Nachbearbeitung N15 («Raum begehen», Ich-Perspektive im 3D): Edge headless über CDP, echte Tasten- und Mausereignisse, frisches
// Beispielprojekt «MFH Musterweg». Nicht Teil von «node --test». Aufruf:
//   node begehen.browser.mjs        (Port 5540, CDP 9540; HLKS_N15_PORT, HLKS_CDP_PORT; HLKS_BUILD wie beim Bauen)
// Der Server läuft mit HLKS_TEST=1 und allen Ablageorten unter %TEMP%\HLKS_CAD\n15_test – nie auf W:. Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT).
//
// Prüft:
//  - Rechtsklick auf einen Raum im Grundriss: Kontextmenü «Raum begehen»; ein Klick wechselt in die 3D-Ansicht im Modus «Begehen»: Kamera im Raum auf 1.7 m über OK Boden,
//    perspektivisch FOV 75°, Blick waagrecht auf die Gebäudeausrichtung, OrbitControls aus, Schnittbereich (N13) abgeschaltet, Einblendung oben links.
//  - Pfeil hoch bewegt nach vorn (Schritt 100 mm), Pfeil links dreht, Umschalt = dreifach, Bild auf/ab = Augenhöhe, gehaltene Taste flüssig, Maus ziehen = umsehen
//    (Nicken begrenzt), Mitte ziehen = verschieben; die Höhe über Boden bleibt beim Gehen gleich.
//  - Navigationswürfel bleibt sichtbar und dreht mit; Klick auf eine Würfelfläche bzw. der Haus-Knopf und Esc beenden das Begehen.
//  - Esc stellt die vorherige Kamera und den Schnittbereich wieder her (Anzeige, Clipping, gespeicherter Wert).
//  - Mit Planwinkel ≠ 0 zeigt der Blick in Richtung «oben» des ausgerichteten Grundrisses; L-förmiger Raum: Standpunkt im Raum.
// Bilder: %TEMP%\HLKS_CAD\n15\*.png (SHOTS ändert den Ordner).
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_N15_PORT ?? 5540);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9540);
if ([5013, 5098, 5130, 5131, 5149, 5150, 5151, 5152, 5153, 5154, 5155].includes(PORT)) { console.error('Dieser Port ist gesperrt (HKAP, Desktop, Abnahmeserver).'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD', 'n15');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'n15_test');
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
const projektOrdner = path.join(ABLAGE, 'Test_N15_Weg 1', 'c_Pläne und Modelle', 'e_KICAD');
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
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'N15', '--geraet', 'n15-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const kern = async (art, name, body) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const befehl = async (name, body) => { const r = await kern('befehl', name, body); if (!r.angenommen) throw new Error(`${name}: ${r.fehler ?? r.meldung}`); return r; };

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgen15-'));
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


// ------------------------------------------------------------------ Tasten (echte Tastenereignisse)
const VK = { ArrowUp: 38, ArrowDown: 40, ArrowLeft: 37, ArrowRight: 39, PageUp: 33, PageDown: 34, Escape: 27 };
async function taste(code, { umschalt = false, halten = 0 } = {}) {
  const p = { code, key: code, windowsVirtualKeyCode: VK[code], nativeVirtualKeyCode: VK[code], modifiers: umschalt ? 8 : 0 };
  await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', ...p });
  if (halten) await warte(halten);
  await send('Input.dispatchKeyEvent', { type: 'keyUp', ...p });
  await warte(220);
}
const begehenDaten = () => ausw(`const d = document.querySelector('${AKTIV3D}')?.dataset.begehen; return d ? JSON.parse(d) : null;`);
const kameraFov = async () => (await kamera())?.fov ?? null;
const hudText = () => ausw(`return document.querySelector('${AKTIV3D} .d3-begehen')?.innerText ?? null;`);
const strecke = (a, b) => Math.hypot(a.x - b.x, a.y - b.y);
const rechte = (az) => [Math.sin(az * GRAD), -Math.cos(az * GRAD)];   // nach rechts, im Projekt (x, y)

// ------------------------------------------------------------------ Modell (aus der Seite)
try {
  await gehe();
  const info = await ausw(`const z = await import('/js/zustand.js');
    const gs = z.derKlasse('Geschoss').map((g) => ({ kz: g.kennzeichen, id: g.id, name: g.name, kote: g.werte.kote, hoehe: g.werte.hoehe })).sort((a, b) => a.kote - b.kote);
    const rs = z.derKlasse('Raum').map((r) => ({ kz: r.kennzeichen, id: r.id, name: r.name, geschoss: r.geschoss, punkte: r.geometrie?.punkte ?? [] }));
    const geb = z.derKlasse('Gebaeude')[0];
    return { gs, rs, geb: geb?.kennzeichen ?? null, planwinkel: geb?.werte?.planwinkel ?? null };`);
  pruef(info.gs.length >= 2 && info.rs.length >= 2, `Beispielprojekt: ${info.gs.length} Geschosse, ${info.rs.length} Räume, Planwinkel ${info.planwinkel}`);
  const eg = info.gs.find((g) => /^EG/i.test(g.kz) || /Erd/i.test(g.name ?? '')) ?? info.gs[1];
  const raeumeEG = info.rs.filter((r) => r.geschoss === eg.kz);
  const raum1 = raeumeEG[0], raum2 = raeumeEG[1];
  const boden = (r) => Math.min(...r.punkte.map((p) => (p.length > 2 ? p[2] : eg.kote)));
  const schwerpunkt = (r) => { let a = 0, cx = 0, cy = 0; const n = r.punkte.length; for (let i = 0; i < n; i++) { const p = r.punkte[i], q = r.punkte[(i + 1) % n]; const k = p[0] * q[1] - q[0] * p[1]; a += k; cx += (p[0] + q[0]) * k; cy += (p[1] + q[1]) * k; } return [cx / (3 * a), cy / (3 * a)]; };
  const imPolygon = (r, x, y) => { let in_ = false; const n = r.punkte.length; for (let i = 0, j = n - 1; i < n; j = i++) { const a = r.punkte[i], b = r.punkte[j]; if ((a[1] > y) !== (b[1] > y) && x < ((b[0] - a[0]) * (y - a[1])) / (b[1] - a[1]) + a[0]) in_ = !in_; } return in_; };
  // Planwinkel des Gebäudes (gespeichert oder gerechnet wie in der Ansicht); Azimut des Blicks = 90° − Planwinkel
  const planwinkelHolen = async () => {
    if (Number.isFinite(info.planwinkel)) return info.planwinkel;
    const a = await kern('abfrage', 'planwinkel', { gebaeude: info.geb });
    return a?.gebaeude?.[0]?.bestimmt?.winkel ?? 0;
  };
  const planwinkel0 = await planwinkelHolen();
  const azimut0 = ((90 - planwinkel0) % 360 + 360) % 360;
  pruef(raum1 && raum2, `zwei Räume im EG: ${raum1?.kz} ${raum1?.name ?? ''}, ${raum2?.kz}`);

  // Ausgangslage: AN-3D mit eingeschaltetem Schnittbereich (Raum 2 + 1.5 m) – er muss nach dem Begehen genau so wieder da sein
  await befehl('ansicht_raum_zeigen', { raum: raum2.kz });
  await warteBis(async () => (await ausw(`const z = await import('/js/zustand.js'); const a = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-3D'); return a?.werte?.schnittbereich?.aktiv === true;`)), 'Schnittbereich im Modell', 8000);
  const sbModell0 = await ausw(`const z = await import('/js/zustand.js'); const a = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-3D'); return a.werte.schnittbereich;`);

  // ================================================================== Kontextmenü im Grundriss
  abschnitt('N15 Rechtsklick auf einen Raum im Grundriss: Kontextmenü «Raum begehen»');
  await zeige('AN-3D');
  await dreiDGeladen();
  await warteBis(async () => (await sbDaten())?.aktiv === true, 'Schnittbereich sichtbar', 10000);
  const sbAnzeige0 = await sbDaten();
  const clip0 = await clipping();
  const kam0 = await kamera();
  pruef(sbAnzeige0?.aktiv === true && clip0?.geclippt > 0 && kam0, `Ausgangslage: Schnittbereich ein (${clip0?.geclippt}/${clip0?.materialien} Materialien geclippt), Kamera ${JSON.stringify(kam0.lage)}`);
  pruef(kam0.fov === 45, `Kamera der Umlaufansicht: FOV ${kam0.fov}°`);

  // Menüband: Knopf «Raum begehen» in der Gruppe «3D-Schnitt», gesperrt ohne gewählten Raum
  await register('Ansicht');
  await auswahlSetzen(null);
  const kB0 = await knopfInfo('ansicht.raumBegehen');
  pruef(kB0 && kB0.gruppe === '3D-Schnitt' && kB0.symbol && kB0.gesperrt, 'Menüband Ansicht › 3D-Schnitt: Knopf «Raum begehen» mit Symbol, ohne Raum gesperrt', JSON.stringify(kB0));
  await auswahlSetzen(raum1.id);
  const kB1 = await knopfInfo('ansicht.raumBegehen');
  pruef(kB1 && !kB1.gesperrt, 'bei gewähltem Raum frei', JSON.stringify(kB1));
  const bandBild = await speichern('n15-menueband', { x: 0, y: 0, width: 1600, height: 190 });
  pruef(bildPruefen(bandBild), 'Prüfbild: Menüband Ansicht mit «Raum begehen»');
  await auswahlSetzen(null);

  const raumMenue = async (r) => {
    await zeige('AN-GR-EG');
    await warteBis(() => ausw(`return !!document.querySelector('.kachel.aktiv svg.gr-svg [data-id]');`), 'Grundriss gezeichnet', 15000);
    await warte(600);
    const m = [r.punkte.reduce((a, p) => a + p[0], 0) / r.punkte.length, r.punkte.reduce((a, p) => a + p[1], 0) / r.punkte.length];
    const bild = await ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); const e = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-GR-EG'); const i = f.alle().find((x) => x.id === e.id).instanz; return i.zuBildschirm(${JSON.stringify(m)});`);
    await klickAn({ x: bild.x, y: bild.y }, 'rechts');
    await warteBis(() => gibt('.kmenu'), 'Kontextmenü im Grundriss', 5000);
    return ausw(`return [...document.querySelectorAll('.kmenu button')].map((b) => b.textContent);`);
  };
  const eintraege = await raumMenue(raum1);
  pruef(eintraege.includes('Raum begehen') && eintraege.includes('Raum im 3D zeigen'), 'Kontextmenü: «Raum begehen» neben «Raum im 3D zeigen»', eintraege.join(' | '));
  pruef(eintraege.includes('Augenhöhe für «Raum begehen» …'), 'und die Augenhöhe ist einstellbar', eintraege.join(' | '));
  pruef(eintraege.indexOf('Raum begehen') === eintraege.indexOf('Raum im 3D zeigen') + 1, 'Reihenfolge: «Raum begehen» direkt nach «Raum im 3D zeigen»');
  const pak0 = await pakete();
  await ausw(`[...document.querySelectorAll('.kmenu button')].find((b) => b.textContent === 'Raum begehen').click(); return 1;`);
  await warteBis(async () => !!(await begehenDaten()), 'Begehen läuft', 15000);
  await warte(700);

  // ================================================================== Anfangslage im Raum
  abschnitt('N15 Anfangslage: Mitte des Raums, 1.7 m über OK Boden, waagrecht auf die Gebäudeausrichtung');
  const b0 = await begehenDaten();
  const sw = schwerpunkt(raum1);
  const innen = imPolygon(raum1, sw[0], sw[1]);
  pruef(innen, `Schwerpunkt von ${raum1.kz} liegt im Raum (Rechteck): Standpunkt = Schwerpunkt`);
  pruef(nahe(b0.x, sw[0], 1) && nahe(b0.y, sw[1], 1), `Standpunkt (${b0.x}, ${b0.y}) = Schwerpunkt (${sw[0].toFixed(1)}, ${sw[1].toFixed(1)})`);
  pruef(b0.raum === raum1.kz && nahe(b0.bodenOk, boden(raum1), 0.5) && nahe(b0.augenhoehe, 1700, 0.5) && nahe(b0.z, boden(raum1) + 1700, 0.5), `Augenhöhe 1700 mm über OK Boden ${boden(raum1)} (z ${b0.z})`);
  pruef(nahe(b0.azimutGrad, azimut0, 0.01) && nahe(b0.nickenGrad, 0, 0.001), `Blick waagrecht, Azimut ${b0.azimutGrad}° = 90° − Planwinkel (${planwinkel0})`);
  const k1 = await kamera();
  pruef(nahe(k1.lage[0], b0.x / 1000, 0.002) && nahe(k1.lage[1], b0.z / 1000, 0.002) && nahe(k1.lage[2], -b0.y / 1000, 0.002), `Kamera sitzt im Raum auf ${k1.lage[1]} m`, JSON.stringify(k1.lage));
  const blickErw = [Math.cos(azimut0 * GRAD), 0, -Math.sin(azimut0 * GRAD)];
  pruef(winkelZwischen(k1.blick, blickErw) < 0.2 && Math.abs(k1.blick[1]) < 0.002, `Blickrichtung ${JSON.stringify(k1.blick)} waagrecht, wie erwartet`);
  pruef(k1.fov === 75, `perspektivisch, FOV ${k1.fov}°`);
  pruef(await ausw(`return document.querySelector('${AKTIV3D}').classList.contains('d3-begehen-an');`), 'Modus «Begehen» an der 3D-Ansicht');
  const hud0 = await hudText();
  pruef(hud0 && hud0.includes(raum1.kz) && hud0.includes('Geschoss') && hud0.includes('Augenhöhe 1.70 m') && hud0.includes('Esc'), 'Einblendung oben links: Raum, Geschoss, Augenhöhe, Tastenhilfe', hud0);
  pruef((raum1.name ? hud0.includes(raum1.name) : true), `Raumname in der Einblendung (${raum1.name ?? 'ohne Namen'})`);
  const hudR = await rechteckVon(`${AKTIV3D} .d3-begehen`), leinB = await rechteckVon(`${AKTIV3D} .d3-leinwand`);
  pruef(hudR.x - leinB.x < 30 && hudR.y - leinB.y < 30, 'Einblendung oben links');
  pruef(!(await sbDaten()), 'Schnittbereich abgeschaltet (kein Quader, keine Griffe)');
  const cl1 = await clipping();
  pruef(cl1.geclippt === 0, `kein Clipping beim Begehen (${cl1.geclippt}/${cl1.materialien})`);
  const sbModell1 = await ausw(`const z = await import('/js/zustand.js'); const a = [...z.zustand.elemente.values()].find((x) => x.kennzeichen === 'AN-3D'); return a.werte.schnittbereich;`);
  pruef(JSON.stringify(sbModell1) === JSON.stringify(sbModell0), 'die gespeicherte Einstellung des Schnittbereichs bleibt unberührt');
  pruef((await pakete()) === pak0, 'Raum begehen ändert nichts am Modell (kein neues Paket)');
  pruef(await gibt(`${AKTIV3D} .d3-wuerfel-flaeche`) && (await rechteckVon(`${AKTIV3D} .d3-wuerfel-flaeche`)).width > 100, 'Navigationswürfel bleibt sichtbar');
  const bildBegehen = await speichern('n15-begehen-raum', { x: leinB.x, y: leinB.y, width: leinB.width, height: leinB.height });
  pruef(bildPruefen(bildBegehen), 'Prüfbild: im Raum, Blick nach vorn');

  // ================================================================== Bewegen
  abschnitt('N15 Bewegen: Pfeile, Umschalt, Bild auf/ab, Maus');
  const nordVor = await ausw(`return document.querySelector('${AKTIV3D} .d3-kompass').dataset.nord;`);
  await taste('ArrowUp');
  const b1 = await begehenDaten();
  const d1 = [b1.x - b0.x, b1.y - b0.y];
  pruef(nahe(Math.hypot(...d1), 100, 1.5) && nahe(d1[0], 100 * Math.cos(azimut0 * GRAD), 1.5) && nahe(d1[1], 100 * Math.sin(azimut0 * GRAD), 1.5), `Pfeil hoch: 100 mm nach vorn (Δ ${d1.map((v) => v.toFixed(1))})`);
  pruef(nahe(b1.z, b0.z, 0.01), 'Höhe über Boden unverändert');
  await taste('ArrowDown');
  const b2 = await begehenDaten();
  pruef(strecke(b2, b0) < 1.5, 'Pfeil runter: zurück an den Ausgangspunkt');
  await taste('ArrowUp', { umschalt: true });
  const b3 = await begehenDaten();
  pruef(nahe(strecke(b3, b2), 300, 2), `Umschalt + Pfeil hoch: 300 mm (${strecke(b3, b2).toFixed(1)})`);
  await taste('ArrowDown', { umschalt: true });
  await taste('ArrowLeft');
  const b4 = await begehenDaten();
  pruef(nahe(b4.azimutGrad - azimut0, 5, 0.01) && strecke(b4, b2) < 1.5, `Pfeil links: 5° nach links gedreht (${b4.azimutGrad}°), Standpunkt bleibt`);
  const nordNach = await ausw(`return document.querySelector('${AKTIV3D} .d3-kompass').dataset.nord;`);
  pruef(nordNach !== nordVor, `der Navigationswürfel dreht mit (Nordmarke ${nordVor}° → ${nordNach}°)`);
  await taste('ArrowRight'); await taste('ArrowRight');
  const b5 = await begehenDaten();
  pruef(nahe(b5.azimutGrad - azimut0, -5, 0.01), `Pfeil rechts zweimal: 5° nach rechts von der Anfangsrichtung (${b5.azimutGrad}°)`);
  await taste('ArrowLeft');
  await taste('ArrowLeft', { umschalt: true });
  const b6 = await begehenDaten();
  pruef(nahe(b6.azimutGrad - azimut0, 15, 0.01), `Umschalt + Pfeil links: 15° (${b6.azimutGrad - azimut0}°)`);
  await taste('ArrowRight', { umschalt: true });
  await taste('ArrowRight');
  await taste('PageUp');
  const b7 = await begehenDaten();
  pruef(nahe(b7.augenhoehe, 1800, 0.5) && nahe(b7.z, boden(raum1) + 1800, 0.5) && strecke(b7, b0) < 1.5, `Bild auf: Augenhöhe 1800 mm (${b7.augenhoehe})`);
  await taste('PageDown'); await taste('PageDown');
  const b8 = await begehenDaten();
  pruef(nahe(b8.augenhoehe, 1600, 0.5), `Bild ab zweimal: 1600 mm (${b8.augenhoehe})`);
  const hud1 = await hudText();
  pruef(hud1.includes('Augenhöhe 1.60 m'), 'Einblendung zeigt die neue Augenhöhe');
  await taste('PageUp');
  // gehaltene Taste: flüssig, etwa 1.4 m/s
  const bH0 = await begehenDaten();
  const t0 = Date.now();
  await taste('ArrowUp', { halten: 1000 });
  const bH1 = await begehenDaten();
  const weg = strecke(bH1, bH0), dauer = (Date.now() - t0) / 1000;
  pruef(weg > 700 && weg < 1800, `Pfeil hoch 1 s gehalten: ${weg.toFixed(0)} mm (≈ 1.4 m/s abzüglich Anlauf, Dauer ${dauer.toFixed(2)} s)`);
  pruef(nahe(bH1.z, bH0.z, 0.01), 'Höhe über Boden bleibt auch beim flüssigen Gehen');
  await taste('ArrowDown', { halten: 1000 });
  // Maus: links ziehen = umsehen
  const mitteL = { x: Math.round(leinB.x + leinB.width / 2), y: Math.round(leinB.y + leinB.height * 0.6) };   // ganze Pixel: die Maus rundet sonst
  const kPos = await kamera();
  const bM0 = await begehenDaten();
  await ziehen(mitteL, { x: mitteL.x, y: mitteL.y - 40 }, 'links', 0, 8);
  const bM1 = await begehenDaten(), kM1 = await kamera();
  pruef(nahe(bM1.nickenGrad - bM0.nickenGrad, 10, 0.1), `Maus 40 px nach oben ziehen: 10° nach oben schauen (${bM1.nickenGrad}°)`);
  pruef(kM1.blick[1] > 0.15 && Math.hypot(kM1.lage[0] - kPos.lage[0], kM1.lage[1] - kPos.lage[1], kM1.lage[2] - kPos.lage[2]) < 0.001, 'die Kamera dreht sich auf der Stelle (OrbitControls sind aus)');
  for (let i = 0; i < 4; i++) await ziehen(mitteL, { x: mitteL.x, y: mitteL.y - 100 }, 'links', 0, 6);   // 4 × 25° = 100° gefordert
  const bM2 = await begehenDaten();
  pruef(nahe(bM2.nickenGrad, 80, 0.01), `Nicken auf 80° begrenzt (${bM2.nickenGrad}°)`);
  for (let i = 0; i < 8; i++) await ziehen(mitteL, { x: mitteL.x, y: mitteL.y + 100 }, 'links', 0, 6);   // 200° gefordert
  pruef(nahe((await begehenDaten()).nickenGrad, -80, 0.01), 'und auf −80° nach unten');
  for (const dy of [100, 100, 100, 20]) await ziehen(mitteL, { x: mitteL.x, y: mitteL.y - dy }, 'links', 0, 6);   // +80°
  const bNick = await begehenDaten();
  pruef(nahe(bNick.nickenGrad, 0, 0.3), `Nicken wieder waagrecht (${bNick.nickenGrad}°)`);
  const az1 = (await begehenDaten()).azimutGrad;
  await ziehen(mitteL, { x: mitteL.x + 80, y: mitteL.y }, 'links', 0, 8);
  const bM3 = await begehenDaten();
  pruef(nahe(((bM3.azimutGrad - az1 + 540) % 360) - 180, -20, 0.05), `Maus 80 px nach rechts ziehen: 20° nach rechts umsehen (${bM3.azimutGrad}°)`);
  pruef(strecke(bM3, bM0) < 1.5, 'Umsehen verschiebt den Standpunkt nicht');
  await ziehen(mitteL, { x: mitteL.x - 80, y: mitteL.y }, 'links', 0, 8);
  // Mitte ziehen: seitlich und hoch
  const bV0 = await begehenDaten();
  await ziehen(mitteL, { x: mitteL.x - 100, y: mitteL.y }, 'mitte', 0, 8);
  const bV1 = await begehenDaten();
  const r = rechte(bV0.azimutGrad);
  pruef(nahe((bV1.x - bV0.x) * r[0] + (bV1.y - bV0.y) * r[1], 500, 3) && nahe(bV1.z, bV0.z, 0.01), `Mitte 100 px nach links ziehen: 500 mm nach rechts (Greifen), Höhe gleich (${strecke(bV1, bV0).toFixed(0)} mm)`);
  await ziehen(mitteL, { x: mitteL.x, y: mitteL.y + 20 }, 'mitte', 0, 6);
  const bV2 = await begehenDaten();
  pruef(nahe(bV2.augenhoehe - bV1.augenhoehe, 100, 1) && strecke(bV2, bV1) < 1.5, `Mitte 20 px nach unten ziehen: 100 mm höher (${bV2.augenhoehe})`);
  await ziehen(mitteL, { x: mitteL.x + 100, y: mitteL.y - 20 }, 'mitte', 0, 8);
  // durch die Wand gehen (keine Kollision): weit nach vorn
  const bW0 = await begehenDaten();
  await taste('ArrowUp', { umschalt: true, halten: 2200 });
  const bW1 = await begehenDaten();
  pruef(strecke(bW1, bW0) > 3000 && !imPolygon(raum1, bW1.x, bW1.y), `keine Kollision: durch die Wand hindurch ausserhalb des Raums (${strecke(bW1, bW0).toFixed(0)} mm)`);
  pruef(nahe(bW1.z, bW0.z, 0.01), 'Höhe über Boden unverändert, auch ausserhalb');
  const bildZurueck = await speichern('n15-begehen-ausserhalb', { x: leinB.x, y: leinB.y, width: leinB.width, height: leinB.height });
  pruef(bildPruefen(bildZurueck), 'Prüfbild: nach dem Gehen durch die Wand');

  // ================================================================== Beenden: Esc
  abschnitt('N15 Esc beendet: vorherige Kamera und Schnittbereich wie vorher');
  await taste('Escape');
  await warte(500);
  pruef(!(await begehenDaten()), 'Esc beendet das Begehen');
  const kE = await kamera();
  pruef(kE.fov === 45 && [0, 1, 2].every((i) => nahe(kE.lage[i], kam0.lage[i], 0.002) && nahe(kE.ziel[i], kam0.ziel[i], 0.002)), `Kamera wie vorher (Lage ${JSON.stringify(kE.lage)}, FOV ${kE.fov}°)`, JSON.stringify([kE, kam0]));
  const sbE = await sbDaten(), clE = await clipping();
  pruef(JSON.stringify(sbE) === JSON.stringify(sbAnzeige0) && clE.geclippt === clip0.geclippt && clE.geclippt > 0, `Schnittbereich wie vorher: Quader zurück, Clipping ${clE.geclippt}/${clE.materialien}`);
  pruef(!(await hudText()) && !(await ausw(`return document.querySelector('${AKTIV3D}').classList.contains('d3-begehen-an');`)), 'Einblendung weg');
  pruef(JSON.stringify(await sbModell()) === JSON.stringify(sbModell0), 'gespeicherter Schnittbereich unverändert');
  const rasterE = await raster('AN-3D');
  pruef(Object.keys(rasterE).length >= 1, `nur das Sichtbare ist wieder wählbar (${Object.keys(rasterE).length} Elemente)`);
  // Pfeiltasten tun danach nichts mehr
  await taste('ArrowUp');
  pruef(JSON.stringify((await kamera()).lage) === JSON.stringify(kE.lage), 'nach dem Beenden bewegen die Pfeiltasten die Kamera nicht mehr');
  // Orbit funktioniert wieder
  const kO0 = await kamera();
  await ziehen({ x: leinB.x + 500, y: leinB.y + 500 }, { x: leinB.x + 580, y: leinB.y + 520 }, 'links', 0, 8);
  const kO1 = await kamera();
  pruef(winkelZwischen(kO0.blick, kO1.blick) > 3, `Umlaufansicht wieder bedienbar (${winkelZwischen(kO0.blick, kO1.blick).toFixed(1)}°)`);
  // vorherige Kamera ohne Schnittbereich: gespeicherte Kamera für die nächsten Prüfungen
  const kam1 = await kamera();

  // ================================================================== Beenden: Haus-Knopf, Würfelfläche
  abschnitt('N15 Haus-Knopf und Klick auf eine Würfelfläche beenden');
  const starten = async (r) => {
    await ausw(`const g = await import('/js/oberflaeche/register.js'); await g.ausfuehren('ansicht.raumBegehen', { raeume: ${JSON.stringify([r.kz])} }); return 1;`);
    await warteBis(async () => (await begehenDaten())?.raum === r.kz, `Begehen in ${r.kz}`, 15000);
    await warte(500);
  };
  await starten(raum1);
  pruef(!(await sbDaten()) && (await clipping()).geclippt === 0, 'zweiter Start: Schnittbereich wieder abgeschaltet');
  const hausR = await rechteckVon(`${AKTIV3D} .d3-haus`);
  await klickAn({ x: hausR.cx, y: hausR.cy }, 'links');
  await warte(600);
  const kH = await kamera();
  pruef(!(await begehenDaten()) && [0, 1, 2].every((i) => nahe(kH.lage[i], kam1.lage[i], 0.002)) && kH.fov === 45, 'Haus-Knopf: Begehen beendet, vorherige Kamera zurück (keine Startansicht)', JSON.stringify([kH.lage, kam1.lage]));
  pruef(JSON.stringify(await sbDaten()) === JSON.stringify(sbAnzeige0), 'Haus-Knopf: Schnittbereich wie vorher');
  await starten(raum1);
  const wuerfelR = await rechteckVon(`${AKTIV3D} .d3-wuerfel-flaeche`);
  await klickAn({ x: wuerfelR.cx, y: wuerfelR.cy }, 'links');
  await warte(900);
  const kF = await kamera();
  pruef(!(await begehenDaten()) && kF.fov === 45, 'Klick auf eine Würfelfläche beendet das Begehen');
  pruef(JSON.stringify(await sbDaten()) === JSON.stringify(sbAnzeige0) && (await clipping()).geclippt > 0, 'Würfelfläche: Schnittbereich wieder da');
  const dist = Math.hypot(kF.lage[0] - kF.ziel[0], kF.lage[1] - kF.ziel[1], kF.lage[2] - kF.ziel[2]);
  pruef(dist > 2, `und die Kamera fährt zur gewählten Fläche (Abstand zum Ziel ${dist.toFixed(1)} m)`);
  // Ziehen am Würfel beim Begehen: ohne Wirkung
  await starten(raum1);
  const bZ0 = await begehenDaten();
  await ziehen({ x: wuerfelR.cx, y: wuerfelR.cy }, { x: wuerfelR.cx - 40, y: wuerfelR.cy + 10 }, 'links', 0, 8);
  const bZ1 = await begehenDaten();
  pruef(bZ1 && strecke(bZ0, bZ1) < 0.5 && nahe(bZ0.azimutGrad, bZ1.azimutGrad, 0.01), 'Ziehen am Würfel beim Begehen ändert nichts (Würfel dreht nur mit)');
  await taste('Escape');

  // ================================================================== Planwinkel ≠ 0
  abschnitt('N15 Gebäude mit Planwinkel ≠ 0: Blick auf die Gebäudeausrichtung');
  await befehl('element_aendern', { element: info.geb, eigenschaften: { planwinkel: 20 } });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Gebaeude')[0].werte.planwinkel === 20;`), 'Planwinkel 20°', 8000);
  await starten(raum1);
  const bP = await begehenDaten(), kP = await kamera();
  pruef(nahe(bP.azimutGrad, 70, 0.01), `Planwinkel 20° → Azimut 70° (${bP.azimutGrad}°)`);
  pruef(winkelZwischen(kP.blick, [Math.cos(70 * GRAD), 0, -Math.sin(70 * GRAD)]) < 0.2, 'Kamera blickt in diese Richtung');
  // «VORNE» des Würfels schaut zur Kamera: die Würfel-Ausrichtung ist −Planwinkel; die Blickrichtung ist Gegenrichtung von VORNE (0,0,1) gedreht um θ = −20°
  const theta = -20 * GRAD;
  pruef(winkelZwischen(kP.blick, [-Math.sin(theta), 0, -Math.cos(theta)]) < 0.2, 'Blick = Gegenrichtung von «VORNE» des Navigationswürfels (Gebäudeausrichtung)');
  await taste('Escape');
  await befehl('element_aendern', { element: info.geb, eigenschaften: { planwinkel: planwinkel0 } });

  // ================================================================== L-förmiger Raum, Augenhöhe als Benutzereinstellung
  abschnitt('N15 L-förmiger Raum: Standpunkt im Raum; Augenhöhe als Benutzereinstellung');
  const x0 = 90000, y0 = 90000;   // weit weg vom Gebäude
  const lPunkte = [[0, 0], [6000, 0], [6000, 1000], [1000, 1000], [1000, 6000], [0, 6000]].map(([x, y]) => [x0 + x, y0 + y]);
  await befehl('element_anlegen', { klasse: 'Raum', kennzeichen: 'R-LTEST', name: 'L-Raum', geschoss: eg.kz, geometrie: { art: 'polygon', punkte: lPunkte }, eigenschaften: { lichteHoehe: 2500 } });
  await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return !!z.derKlasse('Raum').find((r) => r.kennzeichen === 'R-LTEST');`), 'L-Raum im Modell', 8000);
  const lRaum = { kz: 'R-LTEST', punkte: lPunkte };
  const setzeAugen = async (wert) => {
    const rsp = await fetch(`${SERVER}/api/benutzer/einstellungen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ schluessel: 'ansicht3d.augenhoehe', wert }) });
    await ausw(`const b = await import('/js/oberflaeche/benutzer.js'); await b.benutzerLaden(); return 1;`);
    return rsp;
  };
  pruef((await setzeAugen(1500)).ok, 'Benutzereinstellung ansicht3d.augenhoehe = 1500 gespeichert');
  pruef(!(await setzeAugen(100)).ok, 'ungültige Augenhöhe (100 mm) wird abgelehnt');
  await starten(lRaum);
  const bL = await begehenDaten();
  pruef(imPolygon(lRaum, bL.x, bL.y), `Standpunkt (${bL.x}, ${bL.y}) liegt im L-förmigen Raum (der Schwerpunkt läge ausserhalb)`);
  const swL = schwerpunkt(lRaum);
  pruef(!imPolygon(lRaum, swL[0], swL[1]), `Kontrolle: der Schwerpunkt (${swL[0].toFixed(0)}, ${swL[1].toFixed(0)}) liegt ausserhalb`);
  pruef(nahe(bL.augenhoehe, 1500, 0.5) && nahe(bL.z, eg.kote + 1500, 0.5), `Augenhöhe aus der Benutzereinstellung: 1500 mm (${bL.augenhoehe})`);
  const bildL = await speichern('n15-begehen-l-raum', { x: leinB.x, y: leinB.y, width: leinB.width, height: leinB.height });
  pruef(bildPruefen(bildL), 'Prüfbild: L-förmiger Raum');
  await taste('Escape');
  await setzeAugen(null);

  // ================================================================== Ohne 3D-Ansicht
  abschnitt('N15 ohne 3D-Ansicht: «Raum begehen» legt AN-3D an');
  await schliesseAlle();
  await befehl('element_loeschen', { element: 'AN-3D' });
  await warteBis(async () => !(await modellWert('AN-3D', 'art')), 'AN-3D gelöscht', 5000);
  await auswahlSetzen(raum2.id);
  await ausw(`const r = await import('/js/oberflaeche/register.js'); await r.ausfuehren('ansicht.raumBegehen'); return 1;`);
  await warteBis(async () => (await modellWert('AN-3D', 'art')) === '3d', 'AN-3D angelegt', 10000);
  await warteBis(async () => (await begehenDaten())?.raum === raum2.kz, 'Begehen in der neuen Ansicht', 15000);
  await warte(900);
  const bN = await begehenDaten(), kN = await kamera();
  pruef(nahe(bN.augenhoehe, 1700, 0.5) && nahe(kN.lage[1], (boden(raum2) + 1700) / 1000, 0.002), 'neue 3D-Ansicht: Kamera im Raum (nach dem Laden nicht überschrieben)', JSON.stringify([bN, kN.lage]));
  await taste('Escape');

  pruef(fehler.length === 0, 'keine Skriptfehler in der Seite', fehler.join(' | ').slice(0, 400));
  const wichtig = konsole.filter((m) => !/favicon|404|ResizeObserver|WebGL|GPU stall|swiftshader/i.test(m));
  pruef(wichtig.length === 0, 'keine Fehler in der Konsole', wichtig.join(' | ').slice(0, 400));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e?.stack ?? e);
  try { await speichern('n15-abbruch'); } catch { /* egal */ }
}
console.log(`\n${ok} ok, ${schlecht} fehlgeschlagen`);
await beenden(schlecht > 0 ? 1 : 0);
