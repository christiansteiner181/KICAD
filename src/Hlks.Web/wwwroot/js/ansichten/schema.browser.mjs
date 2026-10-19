// Browser-Integrationstest der Schema-Ansicht (Modul 3, Teil C2): Edge headless über CDP, echte Mausklicks und Tastatur, frisches Beispielprojekt
// «MFH Musterweg» (dort gibt es die Schema-Ansicht AN-SCHEMA-SAN). Nicht Teil von «node --test». Der Test startet seinen eigenen Server
// (Port 5162, Build-Ordner %USERPROFILE%\HLKS_CAD_build bzw. HLKS_BUILD; HLKS_BAU ändert ihn):
//   SHOTS=<Ordner> node schema.browser.mjs
// oder gegen einen eigenen Server mit frischem Beispielprojekt: HLKS_TEST_SERVER=http://localhost:5162 node schema.browser.mjs
//
// Ablauf: Schema öffnen (Plangrafik des Kerns, kein Massstab, Banner mit den Hinweisen, Projektbrowser «Schemas») › Knoten und Linienteil wählen
// (Palette) › Knoten ziehen (schema_lage, gerundet, Linien folgen) › Rückgängig › «Automatische Lage» › Element einfügen (nicht platziert, Marke,
// P17) › Verbinden mit Auswahl der Anschlüsse › Ablehnung mit Vorschlag › Trennen (direkte Linie, Linie mit Teilen) › «Im Grundriss zeigen» (auch
// ausgegraut) › «Im Grundriss platzieren» › «Im Schema zeigen» (Grundriss, 3D, Kontextregister) › Neu anordnen › Schema anlegen › Schema im
// Planblatt. Rückgabewert 1, wenn eine Prüfung fehlschlägt.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_M3C2_PORT ?? 5162);
const EIGENER_SERVER = !process.env.HLKS_TEST_SERVER;
const SERVER = process.env.HLKS_TEST_SERVER ?? `http://localhost:${PORT}`;
if (/:(5013|5098|5130|5131|5149)\b/.test(SERVER)) { console.error('Dieser Test läuft nie gegen 5013, 5098, 5130, 5131 oder 5149.'); process.exit(2); }
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9362);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
// Standard: der gemeinsame Build (HLKS_BUILD bzw. %USERPROFILE%\HLKS_CAD_build, wie Directory.Build.props), nie ein alter Ordner eines früheren Auftrags (veralteter Kern: Befund Q2)
const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'hlks_m3c2');
mkdirSync(SHOTS, { recursive: true });
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../..');
const arbeit = mkdtempSync(path.join(tmpdir(), 'hlks_m3c2_'));
// Alle Orte, die der Server sonst beim Benutzer sucht (Einstellungen, «zuletzt», Familienbibliothek, Projektablage), liegen unter %TEMP%: der Test liest und schreibt nie echte Benutzerdateien
const ENV = { HLKS_TEST: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'ausstehend'), HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage') };

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);

// ------------------------------------------------------------------ Server
let serverProzess = null;
function laufen(exe, args, env = {}) {
  return new Promise((res, rej) => {
    const p = spawn(exe, args, { env: { ...process.env, ...ENV, ...env }, stdio: ['ignore', 'pipe', 'pipe'] });
    let aus = ''; p.stdout.on('data', (d) => { aus += d; }); p.stderr.on('data', (d) => { aus += d; });
    p.on('exit', (c) => (c === 0 ? res(aus) : rej(new Error(`${exe} ${args.join(' ')} → ${c}\n${aus}`))));
  });
}
if (EIGENER_SERVER) {
  const projekt = path.join(arbeit, 'c_Plaene und Modelle', 'e_KICAD');
  await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', projekt]);
  serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projekt, '--akteur', 'T', '--geraet', 'm3c2'], { stdio: 'ignore', env: { ...process.env, ...ENV } });
  for (let i = 0; i < 60; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }
}
const kernPost = async (pfad, body) => (await fetch(`${SERVER}${pfad}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();
const kern = (art, name, body, trocken = false) => kernPost(`/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, body);

let proc = null;
async function aufraeumen(code) {
  try { proc?.kill(); } catch { /* egal */ }
  try { serverProzess?.kill(); } catch { /* egal */ }
  await warte(800);
  try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
  process.exit(code);
}

// ------------------------------------------------------------------ Edge
const profil = mkdtempSync(path.join(tmpdir(), 'edgem3c2-'));
proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,950', '--no-first-run', '--enable-unsafe-swiftshader', 'about:blank'], { stdio: 'ignore' });
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent(`${SERVER}/`)}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
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
await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 950, deviceScaleFactor: 1, mobile: false });

const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(path.join(SHOTS, 'm3c2_' + name + '.png'), Buffer.from(r.data, 'base64')); console.log('   Bild:', path.join(SHOTS, 'm3c2_' + name + '.png')); };
const warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(120); } throw new Error('Zeitüberschreitung: ' + t); };
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);

// ------------------------------------------------------------------ echte Mausklicks und Tastatur
const maus = async (typ, x, y, extra = {}) => { await send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra }); };
async function klickAn(x, y, { taste = 'left', n = 1 } = {}) {
  await maus('mouseMoved', x, y); await warte(30);
  for (let i = 1; i <= n; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: taste, buttons: taste === 'left' ? 1 : 2, clickCount: i });
    await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: taste, buttons: 0, clickCount: i });
  }
  await warte(150);
}
/** Ziehen mit gedrückter linker Taste in mehreren Schritten. */
async function ziehe(von, nach, schritte = 8) {
  await maus('mouseMoved', von.x, von.y); await warte(30);
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: von.x, y: von.y, button: 'left', buttons: 1, clickCount: 1 });
  for (let i = 1; i <= schritte; i++) {
    await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: von.x + (nach.x - von.x) * i / schritte, y: von.y + (nach.y - von.y) * i / schritte, button: 'left', buttons: 1 });
    await warte(15);
  }
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: nach.x, y: nach.y, button: 'left', buttons: 0, clickCount: 1 });
  await warte(200);
}
const mitte = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; const r = e.getBoundingClientRect(); return r.width === 0 && r.height === 0 ? null : { x: r.left + Math.min(r.width / 2, 40), y: r.top + r.height / 2 };`);
async function klick(sel, opt = {}) {
  const p = await mitte(sel);
  if (!p) throw new Error('Element nicht sichtbar: ' + sel);
  const oben = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); const t = document.elementFromPoint(${p.x}, ${p.y}); return !!t && (e === t || e.contains(t) || t.contains(e) || (e.closest('label') && e.closest('label').contains(t)));`);
  if (!oben) throw new Error('Element ist verdeckt: ' + sel);
  await klickAn(p.x, p.y, opt);
}
const CODES = { Enter: ['Enter', 13], Escape: ['Escape', 27], ' ': ['Space', 32], Tab: ['Tab', 9] };
async function taste(key, mod = 0) {
  const [code, vk] = CODES[key] ?? [`Key${key.toUpperCase()}`, key.toUpperCase().charCodeAt(0)];
  const text = key === 'Enter' ? '\r' : key === ' ' ? ' ' : undefined;
  await send('Input.dispatchKeyEvent', { type: text ? 'keyDown' : 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers: mod, text });
  await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers: mod });
  await warte(80);
}
async function tippe(sel, text) {
  await klick(sel);
  await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); e.focus(); if (e.select) e.select(); return true;`);
  if (text === '') await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Delete', code: 'Delete', windowsVirtualKeyCode: 46 });
  else await send('Input.insertText', { text });
  await warte(100);
}
const gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const text = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
const alleSchliessen = async () => { for (let i = 0; i < 6 && (await anzahl('#dialogschicht .dlg')) > 0; i++) { await taste('Escape'); await warte(150); } };

// ------------------------------------------------------------------ Hilfen für die Schema-Ansicht
const fenster = (code) => ausw(`const f = await import('/js/fenster.js'); const z = await import('/js/zustand.js'); ${code}`);
const imSchema = (code) => fenster(`const s = f.aktive()?.instanz; if (!s?.istSchema) throw new Error('Keine Schema-Ansicht aktiv'); ${code}`);
const schemaOeffnen = async (kz = 'AN-SCHEMA-SAN') => {
  await fenster(`await f.ansichtOeffnen(${JSON.stringify(kz)}); return 1;`);
  await warteBis(() => imSchema(`return !!s.daten && document.querySelectorAll('.sc-svg g[data-id]').length > 5;`), 'Schema gezeichnet');
  await warte(200);
};
const daten = () => imSchema(`return JSON.parse(JSON.stringify(s.daten));`);
const knotenDaten = async (kz) => (await daten()).knoten.find((k) => k.kennzeichen === kz) ?? null;
/** Bildschirmposition eines Knotens (Mitte seines Symbols). */
const knotenPos = async (kz) => imSchema(`const k = s.daten.knoten.find((x) => x.kennzeichen === ${JSON.stringify(kz)}); if (!k) return null; const p = s.zuBildschirm([k.x, k.y]); return { x: p.x, y: p.y, papier: [k.x, k.y], manuell: k.manuell, platziert: k.platziert, id: k.element };`);
const papierPos = (x, y) => imSchema(`const p = s.zuBildschirm([${x}, ${y}]); return { x: p.x, y: p.y };`);
/** Freie Stelle für ein Symbol im Rechteck [x0, y0, x1, y1] (Papier-mm): weit weg von allen Knoten und Stranglinien. */
function frei(dd, box, belegt = [], abstand = 12) {
  const pts = [...dd.knoten.map((k) => [k.x, k.y]), ...belegt];
  const linienX = dd.spalten.filter((s) => s.art === 'senkrecht').map((s) => s.linie);
  for (let y = box[3]; y >= box[1]; y -= 2) for (let x = box[0]; x <= box[2]; x += 2)
    if (pts.every((q) => Math.hypot(q[0] - x, q[1] - y) >= abstand) && linienX.every((lx) => Math.abs(lx - x) > 5)) return [x, y];
  throw new Error('Keine freie Stelle im Rechteck ' + JSON.stringify(box));
}
/** Punkt auf einer Linie, der weit von allen anderen Linien und von Knoten liegt (damit der Klick eindeutig ist). */
function freiAufLinie(dd, linie, abstand = 3.5, leise = false) {
  const segs = (l) => l.punkte.slice(1).map((q, i) => [l.punkte[i], q]);
  const dist = (px, py, a, b) => { const dx = b[0] - a[0], dy = b[1] - a[1], l2 = dx * dx + dy * dy; const t = l2 ? Math.max(0, Math.min(1, ((px - a[0]) * dx + (py - a[1]) * dy) / l2)) : 0; return Math.hypot(px - a[0] - t * dx, py - a[1] - t * dy); };
  let beste = null, bestD = -1;
  for (const [a, b] of segs(linie)) {
    const n = Math.max(1, Math.floor(Math.hypot(b[0] - a[0], b[1] - a[1])));
    for (let i = 1; i < n; i++) {
      const x = a[0] + (b[0] - a[0]) * i / n, y = a[1] + (b[1] - a[1]) * i / n;
      const andere = Math.min(...dd.linien.filter((l) => l !== linie && JSON.stringify(l.punkte) !== JSON.stringify(linie.punkte)).flatMap(segs).map(([c, d]) => dist(x, y, c, d)), 99);
      const knoten = Math.min(...dd.knoten.map((k) => Math.hypot(k.x - x, k.y - y)), 99);
      const d = Math.min(andere, knoten - 2);
      if (d > bestD) { bestD = d; beste = [x, y]; }
    }
  }
  if (bestD < abstand && !leise) console.log('   Hinweis: der gewählte Punkt liegt nur ' + bestD.toFixed(1) + ' mm von einer anderen Linie');
  if (beste) beste.abstand = bestD;   // Abstand zur nächsten anderen Linie bzw. zum nächsten Symbol (ohne 2 mm Rand)
  return beste;
}
const warteSchema = (fn, t) => warteBis(async () => fn(await daten()), t);
const eltLesen = (kz) => kern('abfrage', 'element_lesen', { element: kz });
const menueTexte = () => ausw(`return [...document.querySelectorAll('.kmenu button')].map((b) => ({ text: b.textContent, gesperrt: b.classList.contains('gesperrt'), titel: b.title }));`);
async function menueKlick(textTeil) {
  const ok2 = await ausw(`const b = [...document.querySelectorAll('.kmenu button')].find((x) => x.textContent.startsWith(${JSON.stringify(textTeil)})); if (!b) return false; b.setAttribute('data-test', 'menue'); return true;`);
  if (!ok2) throw new Error('Menüeintrag fehlt: ' + textTeil + ' – vorhanden: ' + JSON.stringify((await menueTexte()).map((m) => m.text)));
  await klick('.kmenu button[data-test="menue"]');
  await warte(250);
}
const aktiveAnsicht = () => zustandDaten('z.zustand.aktiveAnsicht');
const auswahlKz = () => zustandDaten('z.element(z.zustand.auswahl)?.kennzeichen ?? null');
const befunde = async () => (await kern('abfrage', 'pruefen', {}));

/** Ein Element im Schema einfügen: Dialog, Klasse suchen, Weiter, in die Zelle klicken. */
async function einfuegen(suche, wert, kz, papier) {
  await klick('.sc-knopf[data-werkzeug="einfuegen"]');
  await warteBis(() => gibt('.dlg .sc-einfuegen'), 'Dialog Element einfügen');
  await tippe('.sc-suche', suche);
  await warteBis(async () => (await anzahl(`.sc-wahl input[value="${wert}"]`)) === 1, 'Auswahl gefiltert');
  await klick(`.sc-wahl input[value="${wert}"]`);
  await tippe('.sc-kz', kz);
  await klick('.dlg-knopf.primaer');
  await warteBis(async () => (await imSchema(`return s.modus`)) === 'einfuegen', 'Einfügemodus');
  const p = await papierPos(papier[0], papier[1]);
  await klickAn(p.x, p.y);
  await warteBis(async () => (await knotenDaten(kz)) !== null, `${kz} steht im Schema`);
  await warte(300);
}

try {
// ------------------------------------------------------------------ Start
await warteBis(async () => (await zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 40000);
await warte(1000);
await fenster(`await z.derKlasse('Ansicht').filter((a) => a.werte?.art === 'grundriss').forEach(() => {}); return 1;`);
await schemaOeffnen();
await shot('01_schema');

abschnitt('Schema öffnen: Plangrafik des Kerns, kein Massstab, Banner, Projektbrowser');
const d0 = await daten();
pruef(d0.knoten.length > 40 && d0.linien.length > 40 && d0.spalten.length >= 5 && d0.zeilen.length >= 3, 'Die Abfrage «schema» liefert Knoten, Linien, Spalten und Zeilen', `${d0.knoten.length}/${d0.linien.length}/${d0.spalten.length}/${d0.zeilen.length}`);
const ids = await ausw(`return [...document.querySelectorAll('.sc-svg g.el[data-id]')].map((g) => g.getAttribute('data-id'));`);
pruef(d0.knoten.every((k) => ids.includes(k.element)), 'Jeder Knoten ist ein Teil der Plangrafik mit seiner Element-Id');
pruef(d0.linien.filter((l) => l.elemente.length).every((l) => l.elemente.every((e) => ids.includes(e))), 'Jedes Linienelement ist ein Teil mit seiner Id');
pruef(!(await gibt('.sc-wurzel .gr-masstab')) && !(await gibt('.sc-wurzel .gr-balken')), 'Kein Massstab und kein Massstabsbalken');
pruef(/ohne Massstab/.test(await text('.kachel.aktiv .kachel-steuerung')) && (await gibt('.kachel.aktiv .kachel-steuerung .detailgrad')), 'Steuerleiste: «Schema, ohne Massstab» mit Detailgrad');
// Welche Hinweise der Kern gibt, hängt vom Stand seiner Regeln ab (früher «Ohne Geschoss …» für den Schacht; seit die Schächte im Untergrund als Schnitt stehen:
// «Unter dem untersten Band …», «Abwasserleitungen ohne Aussendurchmesser …»): der Banner zeigt genau die Hinweise der Abfrage.
const hinweiseKern = d0.hinweise ?? [];
pruef(hinweiseKern.length >= 1 && await gibt('.sc-banner:not([hidden])') && new RegExp(`${hinweiseKern.length} Hinweis`).test(await text('.sc-banner')), `Banner zeigt die Hinweise des Kerns (${hinweiseKern.length})`, await text('.sc-banner'));
await klick('.sc-banner-kopf');
const listeText = (await text('.sc-banner-liste')).replace(/\s+/g, ' ');
pruef(hinweiseKern.every((h) => listeText.includes(h.replace(/\s+/g, ' ').slice(0, 30))) && /KS-1/.test(listeText), 'Aufgeklappt steht die ganze Liste im Banner', listeText.slice(0, 300));
await klick('.sc-banner-kopf');
await ausw(`const z = document.querySelector('.pb-zeile[data-key="g:schema"]'); if (z && z.querySelector('.pb-pfeil')?.textContent === '▸') z.click(); return 1;`);
await warte(250);
const pb = await text('#projektbrowser');
pruef(/Schemas \(1\)/.test(pb) && /Prinzipschema Sanitär/.test(pb), 'Projektbrowser: Gruppe «Schemas» mit der Schema-Ansicht', pb);
pruef(!/folgt in Modul/.test(await text('.kachel.aktiv .kachel-inhalt')), 'Kein Platzhalter «folgt in Modul 3» mehr');

abschnitt('Auswählen: Knoten und Linienteil, Palette');
let p = await knotenPos('WT-E.02');
await klickAn(p.x, p.y);
pruef((await auswahlKz()) === 'WT-E.02', 'Klick auf ein Symbol wählt das Element');
await warteBis(() => ausw(`return [...document.querySelectorAll('#eigenschaften input')].some((i) => i.value === 'WT-E.02');`), 'Palette zeigt das Element');
pruef(true, 'Die Eigenschaftenpalette zeigt das gewählte Element');
pruef(await gibt('.sc-svg g[data-id].gewaehlt'), 'Das Symbol ist hervorgehoben');
const dd1 = await daten();
const einzelKandidaten = dd1.linien.filter((l) => l.elemente.length === 1 && l.punkte.some((q, i) => i > 0 && Math.hypot(q[0] - l.punkte[i - 1][0], q[1] - l.punkte[i - 1][1]) >= 10))
  .map((l) => ({ l, pt: freiAufLinie(dd1, l, 0, true) })).filter((x) => x.pt).sort((a, b) => b.pt.abstand - a.pt.abstand);   // die Linie mit dem eindeutigsten Klickpunkt
const einzel = einzelKandidaten[0].l;
// Der Klickpunkt liegt auf der Linie, aber weit von anderen Linien und von Symbolen: die Mitte eines Stücks kann unter einem Symbol liegen (der Kern setzt Knoten
// verschiedener Systeme auch aufeinander) und träfe dann den Knoten statt der Linie.
const lm = einzelKandidaten[0].pt;
p = await papierPos(lm[0], lm[1]);
await klickAn(p.x, p.y);
pruef((await zustandDaten(`z.zustand.auswahl`)) === einzel.elemente[0], `Klick auf eine Linie wählt das Teilelement (${einzel.kennzeichen[0]})`, await auswahlKz());
const leerPos = await papierPos(-5, 170);
await klickAn(leerPos.x, leerPos.y);
pruef((await auswahlKz()) === null, 'Klick auf leere Fläche hebt die Auswahl auf');

abschnitt('Knoten ziehen: schema_lage');
p = await knotenPos('WT-E.02');
const vorher = p.papier;
const spot1 = frei(await daten(), [112, 52, 168, 96]);
await ziehe({ x: p.x, y: p.y }, await papierPos(spot1[0], spot1[1]));
await warteSchema((dd) => dd.knoten.find((k) => k.kennzeichen === 'WT-E.02')?.manuell === true, 'Hand-Lage gesetzt');
let k1 = await knotenDaten('WT-E.02');
pruef(k1.manuell && k1.platziert, 'Nach dem Ziehen hat der Knoten eine Hand-Lage (manuell: true)');
pruef(Math.hypot(k1.x - vorher[0], k1.y - vorher[1]) > 10 && Math.abs(k1.x - spot1[0]) <= 4 && Math.abs(k1.y - spot1[1]) <= 4, 'Er steht an der neuen Stelle (am Ziel der Maus, gerundet bzw. eingerastet)', JSON.stringify([vorher, spot1, k1.x, k1.y]));
pruef((k1.x * 2) % 1 === 0 && (k1.y * 2) % 1 === 0 || Math.abs(k1.x - 40) < 0.1, 'Die Lage ist gerundet (0.5 mm) bzw. auf eine Stranglinie eingerastet', `${k1.x} / ${k1.y}`);
const lagen = await zustandDaten(`z.element('AN-SCHEMA-SAN').werte.lagen`);
pruef(lagen && lagen[k1.element]?.manuell === true && lagen[k1.element].x === k1.x, 'Im Modell steht die Hand-Lage (Ansicht.lagen)', JSON.stringify(lagen));
const d1 = await daten();
const linieAn = d1.linien.find((l) => l.von === k1.element || l.nach === k1.element);
const ende = linieAn.von === k1.element ? linieAn.punkte[0] : linieAn.punkte.at(-1);
pruef(Math.abs(ende[0] - k1.x) < 0.01 && Math.abs(ende[1] - k1.y) < 0.01, 'Die Linie folgt dem Knoten (neu geführt vom Kern)');
pruef(!(await ausw(`return !!document.querySelector('.sc-svg g[data-id="${k1.element}"][transform]');`)), 'Die Vorschau-Verschiebung ist nach der Antwort des Kerns weg');
pruef(/von Hand angeordnet/.test(await text('.sc-status')), 'Statusfeld nennt die Hand-Lage');
await shot('02_nach_ziehen');
await taste('z', 2);   // Strg+Z
await warteSchema((dd) => dd.knoten.find((k) => k.kennzeichen === 'WT-E.02')?.manuell === false, 'Rückgängig');
pruef(true, 'Strg+Z macht das Verschieben rückgängig (automatische Lage)');
p = await knotenPos('WT-E.02');
const spot2 = frei(await daten(), [112, 52, 168, 96]);
await ziehe({ x: p.x, y: p.y }, await papierPos(spot2[0], spot2[1]));
await warteSchema((dd) => dd.knoten.find((k) => k.kennzeichen === 'WT-E.02')?.manuell === true, 'wieder Hand-Lage');

abschnitt('Kontextmenü auf einem Knoten: Automatische Lage');
p = await knotenPos('WT-E.02');
await klickAn(p.x, p.y, { taste: 'right' });
await warteBis(() => gibt('.kmenu'), 'Menü offen');
let m = await menueTexte();
pruef(['Im Grundriss zeigen', 'Automatische Lage', 'Verbinden …', 'Löschen …'].every((t) => m.some((x) => x.text.startsWith(t))), 'Menü: Im Grundriss zeigen, Automatische Lage, Verbinden …, Löschen …', JSON.stringify(m.map((x) => x.text)));
pruef(m.some((x) => x.text.startsWith('Trennen:')), 'Menü: Trennen je Verbindung des Knotens');
pruef(!m.find((x) => x.text === 'Automatische Lage').gesperrt, '«Automatische Lage» ist bei einer Hand-Lage frei');
await menueKlick('Automatische Lage');
await warteSchema((dd) => dd.knoten.find((k) => k.kennzeichen === 'WT-E.02')?.manuell === false, 'automatische Lage');
pruef((await zustandDaten(`z.element('AN-SCHEMA-SAN').werte.lagen`)) == null, 'Die Hand-Lage ist aus dem Modell entfernt');
p = await knotenPos('WT-E.02');
await klickAn(p.x, p.y, { taste: 'right' });
await warteBis(() => gibt('.kmenu'), 'Menü offen');
m = await menueTexte();
pruef(m.find((x) => x.text === 'Automatische Lage').gesperrt && /automatische Anordnung/.test(m.find((x) => x.text === 'Automatische Lage').titel), 'Ohne Hand-Lage ist «Automatische Lage» ausgegraut (mit Hinweis)');
await taste('Escape');

abschnitt('Element einfügen: nicht platziert, Marke, P17');
// freie Stelle in der Zelle EG × <Spalte bei x 112–168> (y 50–100). Welcher Strang dort steht, bestimmt der Kern (Spaltenfolge und -breiten ändern sich mit seinen Anordnungsregeln;
// früher stand hier KW-S2, inzwischen SW-F1): der Test liest ihn aus den Spalten der Abfrage "schema" statt ihn festzuschreiben.
const spalteZelle = (await daten()).spalten.find((c) => c.x <= 112 && c.x + c.breite >= 168);
const spotN = frei(await daten(), [112, 52, 168, 96]);
await einfuegen('Waschtisch', 'k:Waschtisch', 'WT-NEU', spotN);
const neu = await eltLesen('WT-NEU');
pruef(neu.klasse === 'Waschtisch' && !neu.geometrie && neu.geschoss === 'EG', 'Das Element ist angelegt: Geschoss EG, ohne Geometrie', JSON.stringify([neu.geschoss, neu.geometrie]));
const kn = await knotenDaten('WT-NEU');
pruef(kn && kn.platziert === false && kn.manuell === true && Math.abs(kn.x - spotN[0]) <= 4 && Math.abs(kn.y - spotN[1]) <= 4, 'Es steht im Schema an der Klickstelle (Hand-Lage), nicht platziert', JSON.stringify(kn));
pruef(!!spalteZelle && neu.system === spalteZelle.system && neu.teilVon === spalteZelle.kennzeichen, `Strang und System kommen aus der Spalte (${spalteZelle?.kennzeichen}, ${spalteZelle?.system})`, JSON.stringify([neu.system, neu.teilVon, spalteZelle?.kennzeichen]));
pruef((await daten()).spalten.every((c) => c.art !== 'ohneStrang'), 'Das Raster bleibt gleich: keine Zone «ohne Strang» entsteht');
pruef(await ausw(`return !![...document.querySelectorAll('.sc-svg g[data-id="${kn.element}"] path')].find((e) => e.getAttribute('stroke-dasharray'));`), 'Marke: gestrichelter Rahmen des nicht platzierten Elements (vom Kern gezeichnet)');
const bf = await befunde();
pruef((bf.befunde ?? bf).some?.((b) => b.code === 'P17' && b.element === 'WT-NEU'), 'Die Prüfung meldet P17 «nur im Schema» für das neue Element');
await klick('.sc-banner-kopf');
pruef(/Nur im Schema \(nicht platziert\).*WT-NEU/.test(await text('.sc-banner-liste')) && /1 nicht platziert/.test(await text('.sc-status')), 'Banner (aufgeklappt) und Statusfeld nennen das nicht platzierte Element', (await text('.sc-banner-liste')) + ' | ' + (await text('.sc-status')));
await klick('.sc-banner-kopf');
pruef((await auswahlKz()) === 'WT-NEU', 'Das neue Element ist gewählt');
await shot('03_eingefuegt');

abschnitt('Verbinden (mit Auswahl der Anschlüsse), Ablehnung, Trennen');
const spotA = frei(await daten(), [112, 52, 168, 96]);
await einfuegen('Ventil', 'k:Ventil', 'V-A', spotA);
const spotB = frei(await daten(), [112, 52, 168, 96], [spotA]);
await einfuegen('Ventil', 'k:Ventil', 'V-B', spotB);
pruef((await eltLesen('V-A')).anschluesse.length === 2 && !(await eltLesen('V-A')).geometrie, 'Zwei Ventile ohne Geometrie eingefügt (teilVon: Strang der Spalte)');
await klick('.sc-knopf[data-werkzeug="verbinden"]');
pruef((await imSchema(`return s.modus`)) === 'verbinden', 'Werkzeug «Verbinden» ist aktiv');
pruef(/Verbinden: ersten Knoten/.test(await text('#statusleiste')), 'Statusleiste führt durch das Werkzeug');
p = await knotenPos('V-A'); await klickAn(p.x, p.y);
pruef(/zweiten Knoten/.test(await text('#statusleiste')), 'Nach dem ersten Klick: «zweiten Knoten anklicken»');
p = await knotenPos('V-B'); await klickAn(p.x, p.y);
await warteBis(() => gibt('.dlg .sc-anschluss'), 'Auswahl der Anschlüsse');
const paare = await ausw(`return [...document.querySelectorAll('.sc-anschluss input[name="sc-paar"]')].map((i) => i.parentElement.innerText.trim());`);
pruef(paare.length === 4 && paare.some((t) => /V-A\.ein ↔ V-B\.aus/.test(t)), 'Mehrere passende Anschlusspaare: Dialog mit den Kandidaten des Kerns', JSON.stringify(paare));
await ausw(`const i = [...document.querySelectorAll('.sc-anschluss input[name="sc-paar"]')].find((x) => /V-A\\.ein ↔ V-B\\.aus/.test(x.parentElement.innerText)); i.checked = true; return 1;`);
await klick('.dlg-knopf.primaer');
await warteBis(async () => (await eltLesen('V-A')).anschluesse.find((a) => a.name === 'ein').verbundenMit === 'V-B.aus', 'verbunden');
pruef(true, 'Verbunden: V-A.ein ↔ V-B.aus (die Verbindung steht im Modell)');
await warteSchema((dd) => dd.linien.some((l) => !l.elemente.length && l.verbindung), 'direkte Linie');
pruef(true, 'Die direkte Verbindung erscheint als Linie im Schema');
await shot('04_verbunden');

// Ablehnung mit Vorschlag: BW-E.02 hat keinen freien Anschluss
p = await knotenPos('BW-E.02'); await klickAn(p.x, p.y);
p = await knotenPos('V-B'); await klickAn(p.x, p.y);
await warteBis(() => gibt('.meldung.warnung'), 'Meldung');
const mt = await text('.meldung.warnung');
pruef(/Kein freies/.test(mt) && /Vorschlag/.test(mt), 'Ablehnung des Kerns steht mit Vorschlag auf dem Bildschirm', mt);
pruef((await imSchema(`return s.modus`)) === 'verbinden', 'Das Werkzeug bleibt für den nächsten Versuch aktiv');
await taste('Escape');
pruef((await imSchema(`return s.modus`)) === 'auswahl', 'Esc beendet das Werkzeug');


// Trennen: die direkte Linie V-A – V-B (ohne Element, also ohne Id im Plan: Treffer nach der Geometrie)
await klick('.sc-knopf[data-werkzeug="trennen"]');
pruef(/Trennen: eine Linie/.test(await text('#statusleiste')), 'Werkzeug «Trennen» ist aktiv');
const vA = await knotenDaten('V-A'), vB = await knotenDaten('V-B');
const direkt = (await daten()).linien.find((l) => !l.elemente.length && [l.von, l.nach].sort().join() === [vA.element, vB.element].sort().join());
let sMax = 1, lMax = 0;
for (let i = 1; i < direkt.punkte.length; i++) { const l = Math.hypot(direkt.punkte[i][0] - direkt.punkte[i - 1][0], direkt.punkte[i][1] - direkt.punkte[i - 1][1]); if (l > lMax) { lMax = l; sMax = i; } }
const dm = freiAufLinie(await daten(), direkt);
p = await papierPos(dm[0], dm[1]);
await klickAn(p.x, p.y);
await warteBis(async () => (await eltLesen('V-A')).anschluesse.find((a) => a.name === 'ein').verbundenMit == null, 'getrennt');
pruef(true, 'Trennen auf einer direkten Linie (ohne Teilelement): die Verbindung ist aus dem Modell');
await warteSchema((dd) => !dd.linien.some((l) => !l.elemente.length && l.verbindung && [l.von, l.nach].includes(vA.element)), 'Linie weg');
pruef(true, 'Die Linie ist aus dem Schema verschwunden');

// Trennen auf einer Linie mit Teil: zwei Möglichkeiten (davor, danach) → Menü
const anzV = await zustandDaten(`z.zustand.verbindungen.length`);
p = await papierPos(lm[0], lm[1]);
await klickAn(p.x, p.y);
await warteBis(() => gibt('.kmenu'), 'Auswahlmenü');
m = await menueTexte();
pruef(m.length === 2 && m.every((x) => x.text.startsWith('Trennen: ') && x.text.includes('↔')), 'Linie mit Teil: Menü mit den beiden Verbindungen des Teils (davor, danach)', JSON.stringify(m.map((x) => x.text)));
await menueKlick('Trennen:');
await warteBis(async () => (await zustandDaten(`z.zustand.verbindungen.length`)) === anzV - 1, 'eine Verbindung weniger');
pruef(true, 'Gewählt wird nur die eine Verbindung getrennt');
await taste('Escape');
await taste('z', 2);
await warteBis(async () => (await zustandDaten(`z.zustand.verbindungen.length`)) === anzV, 'rückgängig');
pruef(true, 'Strg+Z stellt sie wieder her');
await taste('Escape');

abschnitt('Familie einfügen (ohne Punkt: element_anlegen mit bezuege.familie und familientyp)');
const geladen = await kern('befehl', 'familie_laden', { datei: path.join(REPO, 'tests', 'daten', 'familien', 'test_tstueck.familie.json') });
pruef(geladen.angenommen === true, 'Testfamilie T-Stück geladen (Entwurf, fiktive Werte)', JSON.stringify(geladen.fehler));
const famListe = (await kern('abfrage', 'familien', {})).familien.find((f) => f.kategorie === 'TStueck');
const spotF = frei(await daten(), [112, 52, 168, 96], [spotA, spotB]);
await einfuegen('T-St', `f:${famListe.kennzeichen}|${famListe.typen[0]}`, 'TS-FAM', spotF);
const tf = await eltLesen('TS-FAM');
pruef(tf.klasse === 'TStueck' && !tf.geometrie && tf.bezuege?.familie === famListe.kennzeichen, 'Familienelement ohne Geometrie: Kategorie der Familie, Bezug zur Familie', JSON.stringify([tf.klasse, tf.bezuege]));
pruef(tf.eigenschaften.familientyp?.wert === famListe.typen[0] && tf.anschluesse.length === 3 && tf.anschluesse.every((a) => a.dn), 'Typ gesetzt, Anschlüsse mit Grösse vom Kern nachgeführt', JSON.stringify(tf.anschluesse));
pruef(!!spalteZelle && tf.teilVon === spalteZelle.kennzeichen && (await knotenDaten('TS-FAM'))?.platziert === false, `Es steht in der Spalte ${spalteZelle?.kennzeichen}, nicht platziert`, JSON.stringify([tf.teilVon, spalteZelle?.kennzeichen]));

abschnitt('Schema → Grundriss: «Im Grundriss zeigen»');
await schemaOeffnen();
p = await knotenPos('WC-E.02');
await klickAn(p.x, p.y, { taste: 'right' });
await warteBis(() => gibt('.kmenu'), 'Menü');
m = await menueTexte();
pruef(!m.find((x) => x.text === 'Im Grundriss zeigen').gesperrt, '«Im Grundriss zeigen» ist bei einem platzierten Element frei');
await menueKlick('Im Grundriss zeigen');
await warteBis(async () => (await aktiveAnsicht()) === 'AN-GR-EG', 'Grundriss EG aktiv');
await warteBis(async () => (await auswahlKz()) === 'WC-E.02', 'Element gewählt');
pruef(true, 'Der Grundriss des Geschosses ist aktiv und das Element gewählt');
await warte(500);
const vbText = await ausw(`const s = document.querySelector('.kachel.aktiv .gr-svg'); const v = s.viewBox.baseVal; return { x: v.x, y: v.y, w: v.width, h: v.height };`);
pruef(7500 > vbText.x && 7500 < vbText.x + vbText.w && -9500 > vbText.y && -9500 < vbText.y + vbText.h, 'Der Ausschnitt enthält das Element', JSON.stringify(vbText));
pruef(vbText.w < 9000, 'Es ist auf das Element herangezoomt (nicht der ganze Grundriss)', JSON.stringify(vbText));
await shot('05_im_grundriss');
await schemaOeffnen();
p = await knotenPos('WT-NEU');
await klickAn(p.x, p.y, { taste: 'right' });
await warteBis(() => gibt('.kmenu'), 'Menü');
m = await menueTexte();
const iz = m.find((x) => x.text === 'Im Grundriss zeigen');
pruef(iz.gesperrt && /nicht platziert/.test(iz.titel), 'Nicht platziert: «Im Grundriss zeigen» ist ausgegraut, mit Hinweis', JSON.stringify(iz));
pruef(m.some((x) => x.text.startsWith('Im Grundriss platzieren')), 'Dafür gibt es «Im Grundriss platzieren …»');

abschnitt('Nicht platziertes Element im Grundriss platzieren');
await menueKlick('Im Grundriss platzieren');
await warteBis(async () => (await aktiveAnsicht()) === 'AN-GR-EG', 'Grundriss EG aktiv');
await warteBis(async () => (await ausw(`const b = await import('/js/werkzeuge/basis.js'); return b.werkzeugAktiv();`)) === 'schema-platzieren', 'Werkzeug aktiv');
pruef(/WT-NEU: Punkt anklicken/.test(await text('#statusleiste')), 'Statusleiste: «WT-NEU: Punkt anklicken»');
p = await ausw(`const f = await import('/js/fenster.js'); const i = f.aktive().instanz; return i.zuBildschirm([8500, 9000]);`);
await klickAn(p.x, p.y);
await warteBis(async () => !!(await eltLesen('WT-NEU')).geometrie, 'platziert');
const g = (await eltLesen('WT-NEU')).geometrie;
pruef(g.art === 'punkt' && Math.hypot(g.punkte[0][0] - 8500, g.punkte[0][1] - 9000) < 150, 'Klick im Grundriss setzt den Punkt (element_aendern, geometrie punkt)', JSON.stringify(g));
pruef((await ausw(`const b = await import('/js/werkzeuge/basis.js'); return b.werkzeugAktiv();`)) === null, 'Das Werkzeug endet nach dem Platzieren');
const bf2 = await befunde();
pruef(!(bf2.befunde ?? bf2).some((b) => b.code === 'P17' && b.element === 'WT-NEU'), 'P17 ist behoben');
await schemaOeffnen();
pruef((await knotenDaten('WT-NEU')).platziert === true, 'Im Schema ist der Knoten jetzt platziert (Marke weg)');

abschnitt('Grundriss und 3D → Schema: «Im Schema zeigen»');
await fenster(`await f.ansichtOeffnen('AN-GR-EG'); return 1;`);
await warte(600);
p = await ausw(`const f = await import('/js/fenster.js'); const i = f.aktive().instanz; return i.zuBildschirm([7500, 9500]);`);
await klickAn(p.x, p.y, { taste: 'right' });
await warteBis(() => gibt('.kmenu'), 'Menü im Grundriss');
m = await menueTexte();
pruef(m.length === 1 && m[0].text === 'Im Schema zeigen' && !m[0].gesperrt, 'Rechtsklick im Grundriss: «Im Schema zeigen» (eine Schema-Ansicht zeigt das Element)', JSON.stringify(m));
await menueKlick('Im Schema zeigen');
await warteBis(async () => (await aktiveAnsicht()) === 'AN-SCHEMA-SAN', 'Schema aktiv');
pruef((await auswahlKz()) === 'WC-E.02', 'Das Schema ist aktiv und das Element gewählt');
await warte(400);
const zeigt = await imSchema(`const k = s.daten.knoten.find((x) => x.kennzeichen === 'WC-E.02'); const p = s.zuBildschirm([k.x, k.y]); const r = document.querySelector('.kachel.aktiv .sc-svg').getBoundingClientRect(); const v = document.querySelector('.kachel.aktiv .sc-svg').viewBox.baseVal; return { drin: p.x > r.left && p.x < r.right && p.y > r.top && p.y < r.bottom, breite: v.width };`);
pruef(zeigt.drin && zeigt.breite < 200, 'Das Schema zoomt auf das Element', JSON.stringify(zeigt));
// kein Netzelement (Wand): kein Menü
await fenster(`await f.ansichtOeffnen('AN-GR-EG'); return 1;`);
await warte(400);
p = await ausw(`const f = await import('/js/fenster.js'); const i = f.aktive().instanz; return i.zuBildschirm([6000, 10000]);`);
await klickAn(p.x, p.y, { taste: 'right' });
await warte(300);
pruef(!(await gibt('.kmenu')), 'Auf einer Wand gibt es kein Menü «Im Schema zeigen» (kein Netzelement)');
// 3D: das gewählte Element
await fenster(`await f.ansichtOeffnen('AN-3D'); const w = z.derKlasse('WC').find((e) => e.kennzeichen === 'WC-E.02'); z.auswaehlen(w.id); return 1;`);
await warte(1500);
const c3 = await mitte('.kachel.aktiv .d3-leinwand');
await klickAn(c3.x, c3.y, { taste: 'right' });
await warteBis(() => gibt('.kmenu'), 'Menü im 3D');
m = await menueTexte();
pruef(m.some((x) => x.text === 'Im Schema zeigen' && !x.gesperrt), 'Rechtsklick im 3D: «Im Schema zeigen» für das gewählte Element', JSON.stringify(m));
await menueKlick('Im Schema zeigen');
await warteBis(async () => (await aktiveAnsicht()) === 'AN-SCHEMA-SAN', 'Schema aktiv (aus 3D)');
pruef(true, 'Aus dem 3D wechselt es ins Schema');
// Kontextregister «Ändern | Klasse»
await fenster(`await f.ansichtOeffnen('AN-GR-EG'); const w = z.derKlasse('Waschtisch').find((e) => e.kennzeichen === 'WT-E.02'); z.auswaehlen(w.id); return 1;`);
await warte(500);
await klick('.mb-tab.kontext');
pruef((await gibt('.mb-knopf[data-id="kontext.im-schema-zeigen"]')) && (await gibt('.mb-knopf[data-id="kontext.im-grundriss-zeigen"]')), 'Kontextregister «Ändern | Waschtisch» hat «Im Schema zeigen» und «Im Grundriss zeigen»');
await klick('.mb-knopf[data-id="kontext.im-schema-zeigen"]');
await warteBis(async () => (await aktiveAnsicht()) === 'AN-SCHEMA-SAN', 'Schema aktiv (Register)');
pruef((await auswahlKz()) === 'WT-E.02', 'Der Knopf im Kontextregister führt ins Schema');

abschnitt('Schema neu anordnen');
const handVorher = (await daten()).knoten.filter((k) => k.manuell).length;
pruef(handVorher >= 3, 'Es gibt Hand-Lagen (eingefügte Elemente)', String(handVorher));
await klick('.sc-knopf[data-aktion="neu-anordnen"]');
await warteBis(() => gibt('.dlg'), 'Rückfrage');
pruef(/Alle Hand-Lagen entfernen\?/.test(await text('.dlg .dlg-inhalt')), 'Rückfrage «Alle Hand-Lagen entfernen?»');
await alleSchliessen();
pruef((await daten()).knoten.filter((k) => k.manuell).length === handVorher, 'Abbrechen ändert nichts');
await klick('.sc-knopf[data-aktion="neu-anordnen"]');
await warteBis(() => gibt('.dlg'), 'Rückfrage');
await ausw(`[...document.querySelectorAll('.dlg .dlg-knopf')].find((b) => b.textContent === 'Alle Hand-Lagen entfernen').setAttribute('data-test', 'alle'); return 1;`);
await klick('.dlg-knopf[data-test="alle"]');
await warteSchema((dd) => dd.knoten.every((k) => !k.manuell), 'alle automatisch');
pruef(true, '«Alle Hand-Lagen entfernen»: das ganze Schema ist wieder automatisch angeordnet');
pruef((await zustandDaten(`z.element('AN-SCHEMA-SAN').werte.lagen`)) == null, 'Ansicht.lagen ist leer');
await klick('.sc-knopf[data-aktion="neu-anordnen"]');
await warte(300);
pruef(!(await gibt('.dlg')) && (await ausw(`return [...document.querySelectorAll('.meldung')].some((m) => /keine Hand-Lagen/.test(m.innerText));`)), 'Ohne Hand-Lagen nur ein Hinweis, keine Rückfrage');

abschnitt('Schema anlegen (Register Ansicht) und Gruppe «Schema»');
await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === 'Ansicht').click(); return 1;`);
await warte(200);
pruef(await gibt('.mb-knopf[data-id="ansicht.schema"]'), 'Register Ansicht › Erstellen hat den Knopf «Schema»');
const knoepfe = await ausw(`return ['schema.verbinden','schema.trennen','schema.einfuegen','schema.neu-anordnen'].map((id) => { const k = document.querySelector('.mb-knopf[data-id="' + id + '"]'); return k ? !k.classList.contains('gesperrt') : null; });`);
pruef(knoepfe.every((x) => x === true), 'Register Ansicht › Gruppe «Schema»: Verbinden, Trennen, Element einfügen, Neu anordnen – frei bei aktivem Schema', JSON.stringify(knoepfe));
await klick('.mb-knopf[data-id="schema.verbinden"]');
pruef((await imSchema(`return s.modus`)) === 'verbinden', 'Der Knopf im Register startet das Werkzeug «Verbinden» (dieselbe Aktion wie die Leiste)');
await taste('Escape');
await klick('.mb-knopf[data-id="ansicht.schema"]');
await warteBis(() => gibt('.dlg .sc-anlegen'), 'Dialog Schema anlegen');
pruef(/Strangschema/.test(await text('.sc-anlegen')), 'Dialog nennt: Strangschema, Spalte je Strang, Zeile je Geschoss');
await klick('.sc-anlegen .f-systeme input[value="KW"]');
pruef((await ausw(`return document.querySelector('.sc-anlegen .f-kz').value;`)) === 'AN-SCHEMA-KW', 'Kennzeichen-Vorschlag folgt dem gewählten System (AN-SCHEMA-KW)');
await klick('.dlg-knopf.primaer');
await warteBis(async () => (await aktiveAnsicht()) === 'AN-SCHEMA-KW', 'neues Schema geöffnet', 15000);
const nv = await zustandDaten(`z.element('AN-SCHEMA-KW')`);
pruef(nv.werte.art === 'schema' && nv.werte.schemaart === 'strangschema' && JSON.stringify(nv.werte.systeme) === '["KW"]', 'Ansicht art schema, strangschema, systeme [KW] angelegt', JSON.stringify(nv.werte));
await warteBis(() => imSchema(`return !!s.daten && s.daten.spalten.length > 0;`), 'Schema KW gezeichnet');
const dkw = await daten();
pruef(dkw.spalten.every((s) => s.system === 'KW') && dkw.spalten.length === 3, 'Es zeigt nur die Stränge des Systems KW', JSON.stringify(dkw.spalten.map((s) => s.kennzeichen)));
pruef(/AN-SCHEMA-KW|Schema KW/.test(await text('#projektbrowser')), 'Projektbrowser listet das neue Schema in der Gruppe «Schemas»');
await shot('06_schema_kw');

abschnitt('Schema im Planblatt');
const pb1 = await kern('befehl', 'planblatt_anlegen', { nummer: 'S-1', titel: 'Schema Sanitär', format: 'A2' });
const blattKz = pb1.aenderungen?.find((a) => a.klasse === 'Planblatt')?.kennzeichen ?? 'PB-1';
const platz = await kern('befehl', 'ansicht_platzieren', { planblatt: blattKz, ansicht: 'AN-SCHEMA-SAN' });
pruef(platz.angenommen === true, 'ansicht_platzieren nimmt die Schema-Ansicht', JSON.stringify(platz.fehler));
const pdf = await fetch(`${SERVER}/api/plan/${encodeURIComponent(blattKz)}.pdf`);
const pdfBytes = Buffer.from(await pdf.arrayBuffer());
pruef(pdf.ok && pdfBytes.subarray(0, 4).toString() === '%PDF' && pdfBytes.length > 2000, 'Das Planblatt mit dem Schema wird als PDF ausgegeben', `${pdf.status} ${pdfBytes.length}`);
await ausw(`[...document.querySelectorAll('.mb-tab')].find((t) => t.textContent === 'Ansicht').click(); return 1;`);
await klick('.mb-knopf[data-id="plan.platzieren"]');
await warteBis(() => gibt('.dlg .f-ansicht'), 'Dialog Ansicht platzieren');
pruef(await ausw(`return [...document.querySelectorAll('.f-ansicht option')].some((o) => o.value === 'AN-SCHEMA-KW');`), 'Im Dialog «Ansicht platzieren» wählbar: das Schema, das noch auf keinem Blatt steht');
await alleSchliessen();

abschnitt('Design hell und dunkel');
await schemaOeffnen('AN-SCHEMA-SAN');
await shot('07_dunkel');
await klick('.mb-knopf[data-id="ansicht.design"]');
await warte(500);
await shot('08_hell');
await klick('.mb-knopf[data-id="ansicht.design"]');

abschnitt('Konsole');
pruef(fehler.length === 0, 'Keine Ausnahmen in der Seite', fehler.slice(0, 3).join(' | '));
const echteFehler = konsole.filter((k) => !/Failed to load resource|favicon/.test(k));
pruef(echteFehler.length === 0, 'Keine Fehler in der Konsole', echteFehler.slice(0, 3).join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Ausnahme im Test:', e.message);
  try { await shot('99_fehler'); } catch { /* egal */ }
}
console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
await aufraeumen(schlecht > 0 ? 1 : 0);
