// Browser-Integrationstest des Symbolsatzes im Revit-Stil (Teil S, entwurf/menueband-revit.md): Edge headless über CDP, echte Mausklicks.
// Nicht Teil von «node --test». Der Test baut seine Umgebung selbst auf (Beispielprojekt, Server) und räumt nicht weg:
//   %TEMP%\HLKS_CAD\s_test\ablage\Test_S_Weg 1\c_Pläne und Modelle\e_KICAD   Beispielprojekt (Mcp --beispiel)
//   %TEMP%\HLKS_CAD\s_test\benutzer, \bib, \aus, \zuletzt, \anleitung …       Ablagen des Servers (alles unter %TEMP%, nie auf W:)
// Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT = %TEMP%\HLKS_CAD\s_test\wwwroot), siehe menueband.browser.mjs.
// Aufruf:  node symbole.browser.mjs        (Port 5198, HLKS_S_PORT; Edge-CDP 9542, HLKS_S_CDP; HLKS_BUILD wie beim Bauen, Vorvorgabe %USERPROFILE%\HLKS_CAD_build)
// Umgebung: SHOTS=<Ordner für Bilder> (Vorgabe %TEMP%\HLKS_CAD), HLKS_EDGE
// Prüft: Vorgabe «strich»; Umschalten über Ansicht › Fenster › Paletten (Häkchen «Symbole im Revit-Stil (Probe)») OHNE Neuladen (Menüband, Schnellzugriff, Kopf, Projektbrowser,
// Flyout), gleiche Masse und gleiche Stufen des Menübands, Grösse 32 bzw. 16 je Knopfart, Farben ändern sich mit dem Design ohne Neuladen, Zurückschalten, Einstellung beim
// Server («oberflaeche.symbole»), bleibt nach Neuladen (auch ohne Browserspeicher), ungültiger Wert wird abgelehnt. Bilder: s_*.png.
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_S_PORT ?? 5198);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_S_CDP ?? 9542);
if (PORT < 5196 || PORT > 5199 || CDPPORT < 9540 || CDPPORT > 9549) { console.error('Ports ausserhalb 5196–5199 bzw. CDP 9540–9549: dieser Auftrag nimmt nur diese.'); process.exit(2); }
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 's_test');
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
const ABLAGE = path.join(WURZEL, 'ablage');
for (const d of ['ablage', 'bib', 'aus', 'zuletzt', 'benutzer', 'anleitung', 'anleitung_pruefung']) mkdirSync(path.join(WURZEL, d), { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_S_Weg 1', 'c_Pläne und Modelle', 'e_KICAD');
const MCP = path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll');
{
  const r = spawnSync(dotnet, [MCP, '--beispiel', projektOrdner], { encoding: 'utf8' });
  if (r.status !== 0) { console.error('Beispielprojekt nicht angelegt', r.stdout, r.stderr); process.exit(2); }
}
cpSync(path.resolve(hier, '../..'), path.join(WURZEL, 'wwwroot'), { recursive: true });
const env = { ...process.env, HLKS_WEBROOT: path.join(WURZEL, 'wwwroot'), HLKS_TEST: '1', HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: path.join(WURZEL, 'bib'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(WURZEL, 'aus'), HLKS_ZULETZT: path.join(WURZEL, 'zuletzt'), HLKS_BENUTZER: path.join(WURZEL, 'benutzer'), HLKS_ANLEITUNG: path.join(WURZEL, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(WURZEL, 'anleitung_pruefung') };
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'S', '--geraet', 's-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const einstellungen = async () => (await (await fetch(`${SERVER}/api/benutzer/einstellungen`)).json()).werte ?? {};

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edges-'));
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
let aktuelleBreite = 1920;
const breite = async (w) => { aktuelleBreite = w; await send('Emulation.setDeviceMetricsOverride', { width: w, height: 1000, deviceScaleFactor: 1, mobile: false }); await warte(400); };
await breite(1920);
const shot = async (name, hoehe = 260) => {
  const r = await send('Page.captureScreenshot', { format: 'png', clip: { x: 0, y: 0, width: aktuelleBreite, height: hoehe, scale: 1 } });
  const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); return f;
};
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };
const appBereit = () => warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 30000);
const gehe = async (design = 'dunkel') => {
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { if (!localStorage.getItem('hlks.design')) localStorage.setItem('hlks.design', JSON.stringify('${design}')); } catch (e) {}` }).catch(() => {});
  await send('Page.navigate', { url: `${SERVER}/` });
  await appBereit();
  await warte(700);
};
const neuLaden = async () => { await send('Page.reload'); await warte(300); await appBereit(); await warte(500); };
const mitte = (selektor, index = 0) => ausw(`const e = [...document.querySelectorAll(${JSON.stringify(selektor)})].filter((x) => x.getBoundingClientRect().width > 0)[${index}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
const klickAn = async (pos, warten = 300) => {
  await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: pos.x, y: pos.y });
  await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: pos.x, y: pos.y, button: 'left', buttons: 1, clickCount: 1 });
  await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: pos.x, y: pos.y, button: 'left', buttons: 0, clickCount: 1 });
  await warte(warten);
};
const klick = async (selektor, index = 0, warten = 300) => { const pos = await mitte(selektor, index); if (!pos) throw new Error('Element nicht gefunden: ' + selektor); await klickAn(pos, warten); };
const anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
const registerWahl = async (name) => { await ausw(`const t = [...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(name)}); if (!t) throw new Error('Register fehlt: ${name}'); t.click(); return 1;`); await warte(300); };
const info = () => ausw(`const m = await import('/js/oberflaeche/menueband.js'); return m.bandInfo();`);
const designSetzen = (d) => ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}, false); return 1;`).then(() => warte(250));
const menueTexte = () => ausw(`return [...document.querySelectorAll('.kmenu button')].map((b) => b.textContent.replace(/\\s+/g, ' ').trim());`);
const esc = async () => { await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 }); await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 }); await warte(200); };

/** Öffnet die Liste Ansicht › Fenster › Paletten (echter Klick), liefert die Texte der Einträge. */
async function paletteListe() {
  await registerWahl('Ansicht');
  const id = 'ansicht.paletten';
  if (!(await mitte(`#menueband .mb-feld .mb-knopf[data-id="${id}"]`))) {
    // Gruppe «Fenster» zusammengeklappt: über ihren Z-Knopf
    await klick('#menueband .mb-feld .mb-zu[data-gruppe="Fenster"]');
  }
  await klick(`#menueband .mb-knopf[data-id="${id}"]`);
  await warteBis(async () => (await anzahl('.kmenu')) > 0, 'Liste der Paletten', 5000);
  return menueTexte();
}
const SCHALTER = 'Symbole im Revit-Stil (Probe)';
async function schalterKlicken() {
  const t = await paletteListe();
  const i = t.findIndex((x) => x.includes(SCHALTER));
  if (i < 0) throw new Error('Schalter fehlt: ' + JSON.stringify(t));
  await klick('.kmenu button', i, 500);
  return t;
}
const zaehl = () => ausw(`return { revit: document.querySelectorAll('svg.sym-rv').length, strichBekannt: document.querySelectorAll('svg.sym[data-sym]:not(.sym-rv)').length, alle: document.querySelectorAll('svg.sym[data-sym]').length, stil: document.documentElement.dataset.symbole ?? null, speicher: (() => { try { return localStorage.getItem('hlks.symbolstil'); } catch { return null; } })() };`);
/** Messung des Layouts: Stufen, Gruppenbreiten, Grössen aller Symbole im Band */
const layout = () => ausw(`
  const m = await import('/js/oberflaeche/menueband.js'); const i = m.bandInfo();
  const b = document.getElementById('menueband').getBoundingClientRect();
  return { stufen: i.gruppen.map((g) => g.stufe).join(''), register: i.register, band: [Math.round(b.width), Math.round(b.height)],
    gruppen: [...document.querySelectorAll('#menueband .mb-feld > .mb-gruppe')].map((g) => Math.round(g.getBoundingClientRect().width)),
    symbole: [...document.querySelectorAll('#menueband .mb-feld svg.sym')].map((s) => { const r = s.getBoundingClientRect(); return Math.round(r.width) + 'x' + Math.round(r.height); }) };`);
const knopfSymbole = () => ausw(`
  return [...document.querySelectorAll('#menueband .mb-feld .mb-knopf svg.sym-rv')].map((s) => ({ art: s.closest('.mb-knopf').classList.contains('gross') ? 'gross' : 'klein', viewBox: s.getAttribute('viewBox'), w: Math.round(s.getBoundingClientRect().width) }));`);

try {
  await gehe('dunkel');
  await ausw(`window.__fehler = []; window.addEventListener('error', (e) => window.__fehler.push(String(e.message))); window.__marke = 4711; return 1;`);

  // ================================================================== 1. Vorgabe
  abschnitt('1. Vorgabe: Strichsymbole, Einstellung nicht gesetzt');
  {
    const z = await zaehl();
    pruef(z.revit === 0 && z.alle > 20 && z.stil === 'strich', 'ohne Einstellung gelten die Strichsymbole', JSON.stringify(z));
    pruef(!('oberflaeche.symbole' in (await einstellungen())), 'beim Server ist «oberflaeche.symbole» nicht gesetzt');
    pruef(await ausw(`const s = document.querySelector('#menueband .mb-feld svg.sym'); return s.getAttribute('stroke') === 'currentColor' && !s.classList.contains('sym-rv');`), 'die Symbole im Band sind einfarbige Striche (currentColor)');
    const t = await paletteListe();
    pruef(t.length === 5 && t[4].includes(SCHALTER) && !t[4].startsWith('✓'), 'Ansicht › Paletten: Eintrag «Symbole im Revit-Stil (Probe)» am Ende, ohne Häkchen', JSON.stringify(t));
    pruef((await anzahl('.mb-tab')) === 10, 'kein neues Register');
    await esc();
    pruef((await anzahl('.kmenu')) === 0, 'Esc schliesst die Liste');
  }
  const vorher = {};
  for (const reg of ['Ansicht', 'Gebäudetechnik', 'Architektur', 'Ändern']) { await registerWahl(reg); vorher[reg] = await layout(); }
  await registerWahl('Gebäudetechnik');
  await shot('s_gebaeudetechnik_strich_dunkel', 215);

  // ================================================================== 2. Umschalten ohne Neuladen
  abschnitt('2. Umschalten über Ansicht › Fenster › Paletten, ohne Neuladen');
  await schalterKlicken();
  {
    const z = await zaehl();
    pruef(z.revit > 40 && z.strichBekannt === 0 && z.stil === 'revit', 'alle bekannten Symbole sind jetzt im Revit-Stil (Band, Schnellzugriff, Kopf, Projektbrowser …)', JSON.stringify(z));
    pruef((await ausw('return window.__marke;')) === 4711, 'die Seite wurde nicht neu geladen');
    pruef((await einstellungen())['oberflaeche.symbole'] === 'revit', 'beim Server steht oberflaeche.symbole = revit', JSON.stringify(await einstellungen()));
    pruef(z.speicher === '"revit"', 'Zwischenspeicher im Browser (gegen Flackern)', String(z.speicher));
    pruef((await anzahl('#schnellzugriff svg.sym-rv')) >= 4, 'Schnellzugriff trägt die neuen Symbole');
    pruef((await anzahl('#hilfe-knopf svg.sym-rv')) === 1, 'Hilfe-Symbol «?» im Kopf ist getauscht');
    pruef((await anzahl('#palette-browser .pb-sym svg.sym-rv')) > 3, 'Projektbrowser: Baumsymbole getauscht', String(await anzahl('#palette-browser .pb-sym svg')));
    const t = await paletteListe();
    pruef(t[4].startsWith('✓') && t[4].includes(SCHALTER), 'die Liste zeigt jetzt das Häkchen', JSON.stringify(t));
    await esc();
  }

  abschnitt('3. Gleiche Masse und gleiche Stufen wie vorher (nichts am Layout)');
  for (const reg of ['Ansicht', 'Gebäudetechnik', 'Architektur', 'Ändern']) {
    await registerWahl(reg);
    const n = await layout();
    const v = vorher[reg];
    pruef(n.stufen === v.stufen && JSON.stringify(n.gruppen) === JSON.stringify(v.gruppen) && JSON.stringify(n.symbole) === JSON.stringify(v.symbole) && JSON.stringify(n.band) === JSON.stringify(v.band), `${reg}: Stufen, Gruppenbreiten, Symbolgrössen und Bandgrösse unverändert (${n.stufen})`, JSON.stringify({ vorher: v, nachher: n }));
  }
  await registerWahl('Gebäudetechnik');
  {
    const s = await knopfSymbole();
    const gross = s.filter((x) => x.art === 'gross'), klein = s.filter((x) => x.art === 'klein');
    pruef(gross.length > 3 && gross.every((x) => x.viewBox === '0 0 32 32' && x.w === 32), 'große Knöpfe: 32-px-Zeichnung (viewBox 0 0 32 32)', JSON.stringify(gross.slice(0, 3)));
    await breite(1366); await registerWahl('Ansicht');
    const s2 = await knopfSymbole();
    const kl = s2.filter((x) => x.art === 'klein');
    pruef(kl.length > 0 && kl.every((x) => x.viewBox === '0 0 16 16' && x.w === 16), `kleine und mittlere Knöpfe: eigene 16-px-Zeichnung (${kl.length} gezählt bei 1366 px)`, JSON.stringify(kl.slice(0, 3)));
    await breite(1920);
    void klein;
  }

  abschnitt('4. Farben: Medienfarben, hell und dunkel, ohne Neuladen');
  {
    const farbe = (design) => ausw(`
      const m = await import('/js/oberflaeche/symbole-revit.js'); const d = await import('/js/oberflaeche/design.js'); d.designSetzen(${JSON.stringify(design)}, false);
      const h = document.createElement('div'); h.innerHTML = m.revitSymbol('rohr', 32) + m.revitSymbol('formteil', 32) + m.revitSymbol('wand', 32); document.body.appendChild(h);
      const c = (sel, p) => getComputedStyle(h.querySelector(sel))[p];
      const r = { vl: c('.fvl', 'fill'), san: c('.fsa', 'fill'), kontur: c('svg.sym-rv', 'stroke'), g2: c('.f2', 'fill') };
      h.remove(); return r;`);
    const dunkel = await farbe('dunkel'); await warte(150);
    const hell = await farbe('hell'); await warte(150);
    pruef(dunkel.vl === 'rgb(255, 90, 82)' && hell.vl === 'rgb(208, 49, 45)', 'Heizung Vorlauf: Rot, im dunklen Design etwas heller', JSON.stringify({ dunkel: dunkel.vl, hell: hell.vl }));
    pruef(dunkel.san === 'rgb(73, 196, 99)' && hell.san === 'rgb(46, 139, 58)', 'Sanitär: Grün (hell und dunkel)', JSON.stringify({ dunkel: dunkel.san, hell: hell.san }));
    pruef(dunkel.kontur !== hell.kontur && dunkel.g2 !== hell.g2, 'Kontur und Grautöne wechseln mit dem Design', JSON.stringify({ dunkel, hell }));
    await shot('s_gebaeudetechnik_revit_hell', 215);
    await designSetzen('dunkel');
    await registerWahl('Gebäudetechnik');
    await shot('s_gebaeudetechnik_revit_dunkel', 215);
    await registerWahl('Ändern'); await shot('s_aendern_revit_dunkel', 215);
    await registerWahl('Koordination'); await shot('s_koordination_revit_dunkel', 215);
  }

  abschnitt('5. Zusammengeklappte Gruppen (Flyout) und «» weitere» in schmalen Fenstern');
  {
    let z = null, reg = null, weitere = null, wz = 0;
    for (const w of [420, 380, 340, 300, 260, 220, 180, 140]) {
     if (z) break;
     await breite(w);
     for (const r of ['Gebäudetechnik', 'Ändern', 'Ansicht']) {
      await registerWahl(r);
      const i = await info();
      if (!z && i.gruppen.some((g) => g.stufe === 'Z')) { z = i.gruppen.find((g) => g.stufe === 'Z'); reg = r; wz = w; }
      if (!weitere && i.weitere > 0) weitere = r;
      if (z) break;
     }
    }
    pruef(!!z, 'in schmalen Fenstern steht in einem Register eine Gruppe in Stufe Z (' + (reg ?? 'keines') + ' bei ' + wz + ' px)');
    if (z) {
      await breite(wz); await registerWahl(reg);
      await klick(`.mb-feld .mb-zu[data-gruppe="${z.name}"]`);
      pruef((await anzahl('.mb-flyout .mb-knopf svg.sym-rv')) > 0 && (await anzahl('.mb-zu svg.sym-rv')) > 0, 'Flyout der Gruppe «' + z.name + '» und Z-Knopf zeigen die Symbole im Revit-Stil');
      await shot('s_flyout_revit', 330);
      await esc();
    }
    await breite(420);
    for (const r of ['Ansicht', 'Architektur', 'Hülle', 'Einfügen', 'Beschriften', 'Verwalten', 'Gebäudetechnik', 'Ändern', 'Koordination']) { await registerWahl(r); if ((await info()).weitere > 0) { weitere = r; break; } }
    if (weitere) {
      await registerWahl(weitere);
      await klick('.mb-feld .mb-weitere');
      pruef((await anzahl('.mb-flyout.weitere .mb-knopf svg.sym-rv')) > 0, '«» weitere» (Register ' + weitere + '): Flyout zeigt die Symbole im Revit-Stil');
      await esc();
    }
    await breite(1920);
  }

  abschnitt('6. Zurückschalten auf den Strich, ohne Neuladen');
  await schalterKlicken();
  {
    const z = await zaehl();
    pruef(z.revit === 0 && z.stil === 'strich' && z.alle > 20, 'wieder alle Symbole als Strich', JSON.stringify(z));
    pruef((await ausw('return window.__marke;')) === 4711, 'die Seite wurde nicht neu geladen');
    pruef((await einstellungen())['oberflaeche.symbole'] === 'strich', 'beim Server steht oberflaeche.symbole = strich', JSON.stringify(await einstellungen()));
    await registerWahl('Ansicht');
    const n = await layout();
    pruef(n.stufen === vorher.Ansicht.stufen && JSON.stringify(n.symbole) === JSON.stringify(vorher.Ansicht.symbole), 'Layout wie zu Beginn');
  }

  abschnitt('7. Die Wahl bleibt nach dem Neuladen');
  await schalterKlicken();   // wieder revit
  await neuLaden();
  await warteBis(async () => (await zaehl()).revit > 40, 'Revit-Symbole nach dem Neuladen', 8000);
  {
    const z = await zaehl();
    pruef(z.stil === 'revit' && z.strichBekannt === 0, 'nach dem Neuladen gilt der Revit-Stil weiter', JSON.stringify(z));
  }
  await ausw(`localStorage.removeItem('hlks.symbolstil'); return 1;`);
  await neuLaden();
  await warteBis(async () => (await zaehl()).revit > 40, 'Revit-Symbole aus der Benutzereinstellung (ohne Browserspeicher)', 8000);
  pruef(true, 'ohne Browserspeicher holt der Server die Wahl (Benutzereinstellung gilt auch auf einem anderen Rechner)');
  await schalterKlicken();   // zurück auf strich
  await neuLaden();
  await warte(800);
  {
    const z = await zaehl();
    pruef(z.revit === 0 && z.stil === 'strich', 'zurückgestellt bleibt der Strich auch nach dem Neuladen', JSON.stringify(z));
    pruef((await einstellungen())['oberflaeche.symbole'] === 'strich', 'Server: strich');
  }

  abschnitt('8. Server lehnt ungültige Werte ab');
  {
    const r = await fetch(`${SERVER}/api/benutzer/einstellungen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ schluessel: 'oberflaeche.symbole', wert: 'bunt' }) });
    pruef(r.status === 400, `«bunt» wird abgelehnt (HTTP ${r.status})`);
    pruef((await einstellungen())['oberflaeche.symbole'] === 'strich', 'der alte Wert bleibt');
    const r2 = await fetch(`${SERVER}/api/benutzer/einstellungen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ schluessel: 'oberflaeche.symbole', wert: null }) });
    pruef(r2.ok && !('oberflaeche.symbole' in (await einstellungen())), 'null setzt die Einstellung zurück (Vorgabe strich)');
  }

  abschnitt('9. Keine Fehler in der Seite');
  const seitenfehler = (await ausw('return window.__fehler ?? [];')) ?? [];
  pruef(fehler.length === 0 && seitenfehler.length === 0, 'keine Ausnahmen in der Seite', JSON.stringify([...fehler, ...seitenfehler]).slice(0, 600));
  const echteKonsole = konsole.filter((k) => !/Failed to load resource|favicon|net::ERR/.test(k));
  pruef(echteKonsole.length === 0, 'keine Konsolenfehler', echteKonsole.join(' | ').slice(0, 600));
  console.log('   Bilder in', SHOTS, '(s_*.png)');
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e.stack ?? e);
} finally {
  console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
  try { ws.close(); } catch { /* egal */ }
  proc.kill(); server.kill();
  process.exit(schlecht > 0 ? 1 : 0);
}
