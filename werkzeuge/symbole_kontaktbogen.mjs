// Kontaktbogen der Symbole (Teil S, entwurf/menueband-revit.md): alle Symbole in 32 und 16 px, hell und dunkel, alt (Strich) neben neu (Revit-Stil),
// dazu das Menüband der Register Ändern, Architektur, Gebäudetechnik, Ansicht und Koordination bei 1920 px, alt und neu.
// Edge headless über CDP (wie die Browser-Tests). Ausgabe lokal, keine Projektdaten:  %USERPROFILE%\HLKS_CAD_kontaktbogen\
//   kontaktbogen.html                      alles in einer Seite (Raster, Menübänder), öffnen mit Doppelklick
//   symbole-hell-1.png … symbole-dunkel-4.png   Raster je Design, 40 Symbole je Bild; je Symbol: alt 32 · neu 32 · alt 16 · neu 16 · neu 16 vierfach · neu 32 zweifach vergrössert
//   menue-<register>-<alt|neu>-<hell|dunkel>.png   Menüband bei 1920 px
// Aufruf:  node werkzeuge/symbole_kontaktbogen.mjs [--ohne-menue] [--ausgabe <Ordner>]
// Das Raster braucht keinen Server. Die Menüband-Bilder starten einen eigenen Server (Port 5196, Edge-CDP 9540; HLKS_KB_PORT, HLKS_KB_CDP) mit
// einem Beispielprojekt unter %TEMP% (HLKS_TEST=1, alle Ablagen unter %TEMP%; nichts auf W:). Build: HLKS_BUILD (Vorgabe %USERPROFILE%\HLKS_CAD_build\symbole).
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, readFileSync, cpSync, existsSync } from 'node:fs';
import { tmpdir, homedir } from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const hier = path.dirname(fileURLToPath(import.meta.url));
const WWW = path.resolve(hier, '../src/Hlks.Web/wwwroot');
const argv = process.argv.slice(2);
const ohneMenue = argv.includes('--ohne-menue');
const AUS = argv.includes('--ausgabe') ? path.resolve(argv[argv.indexOf('--ausgabe') + 1]) : path.join(process.env.USERPROFILE ?? homedir(), 'HLKS_CAD_kontaktbogen');
const PORT = Number(process.env.HLKS_KB_PORT ?? 5196);
const CDPPORT = Number(process.env.HLKS_KB_CDP ?? 9540);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? homedir(), 'HLKS_CAD_build', 'symbole');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', 'kb_test');
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
mkdirSync(AUS, { recursive: true });

// ------------------------------------------------------------------ Symbole
const imp = (p) => import(pathToFileURL(path.join(WWW, p)).href);
const strich = await imp('js/oberflaeche/symbole.js');
const wz = await imp('js/werkzeuge/symbole.js');
const rv = await imp('js/oberflaeche/symbole-revit.js');
const namen = [...strich.SYMBOLNAMEN_STRICH, ...wz.SYMBOLNAMEN];
const strichSvg = (n, px) => wz.symbolWz(n, px) ?? strich.symbol(n, px);   // Node: Stil bleibt «strich»
const fehlend = namen.filter((n) => !rv.hatRevitFassung(n));
if (fehlend.length) console.log('Hinweis: keine Revit-Fassung (Strich bleibt):', fehlend.join(' '));

const css = readFileSync(path.join(WWW, 'css/symbole-revit.css'), 'utf8');
const hellVars = css.match(/:root\s*\{([^}]*)\}/)[1];
const dunkelVars = css.match(/:root\[data-design="dunkel"\]\s*\{([^}]*)\}/)[1];
const regeln = css.slice(css.indexOf('svg.sym-rv {'));
const DESIGN = { hell: { bg: '#f7f7f7', text: '#1f1f1f', vars: hellVars }, dunkel: { bg: '#3b4453', text: '#f5f5f5', vars: dunkelVars } };

const eigenstaendig = (n, design, k = 16) => {
  const inhalt = rv.revitInhalt(n, k === 16);
  if (!inhalt) return '';
  return `<svg xmlns="http://www.w3.org/2000/svg" class="sym-rv" viewBox="0 0 ${k} ${k}" width="${k}" height="${k}"><style>svg{${DESIGN[design].vars}}${regeln}</style>${inhalt}</svg>`;
};
const esc = (t) => String(t).replace(/&/g, '&amp;').replace(/"/g, '&quot;').replace(/</g, '&lt;');

const JE_SEITE = 40;
const seiten = [];
for (let i = 0; i < namen.length; i += JE_SEITE) seiten.push(namen.slice(i, i + JE_SEITE));

function karte(n, design) {
  const neu32 = rv.revitSymbol(n, 32) ?? '<span class="fehlt">–</span>';
  const neu16 = rv.revitSymbol(n, 16) ?? '<span class="fehlt">–</span>';
  const lupe = rv.hatRevitFassung(n) ? `<canvas class="lupe" width="16" height="16" data-g="16" data-svg="${esc(eigenstaendig(n, design, 16))}"></canvas><canvas class="lupe" width="32" height="32" data-g="32" data-svg="${esc(eigenstaendig(n, design, 32))}"></canvas>` : '';
  return `<div class="karte"><div class="nm">${n}</div><div class="zeile">${strichSvg(n, 32)}${neu32}<span class="ab"></span>${strichSvg(n, 16)}${neu16}<span class="ab"></span>${lupe}</div></div>`;
}

function seitenHtml(design, nr) {
  const k = seiten[nr].map((n) => karte(n, design)).join('');
  return `<section class="seite ${design}" id="s-${design}-${nr + 1}"><h2>Symbole ${design === 'hell' ? 'helles' : 'dunkles'} Design – Seite ${nr + 1} von ${seiten.length}</h2><div class="kopf">je Symbol: alt 32 · neu 32 · alt 16 · neu 16 · neu 16 vierfach · neu 32 zweifach (Pixel)</div><div class="raster">${k}</div></section>`;
}

const SEITENCSS = `
body{margin:0;font:12px "Segoe UI",system-ui,sans-serif;background:#888}
.hell{${hellVars};background:${DESIGN.hell.bg};color:${DESIGN.hell.text}}
.dunkel{${dunkelVars};background:${DESIGN.dunkel.bg};color:${DESIGN.dunkel.text}}
${regeln}
.seite{width:1500px;box-sizing:border-box;padding:10px 14px 14px;margin:0 0 12px}
.seite h2{margin:0 0 2px;font-size:15px}.kopf{opacity:.7;margin:0 0 8px}
.raster{display:grid;grid-template-columns:repeat(5,1fr);gap:6px}
.karte{border:1px solid rgba(128,128,128,.35);padding:3px 6px 5px;border-radius:2px}
.nm{font-size:11px;opacity:.85;margin-bottom:3px;overflow:hidden;white-space:nowrap}
.zeile{display:flex;align-items:center;gap:6px}
.zeile svg{flex:none;display:block}.hell .zeile svg:not(.sym-rv){color:#1f1f1f}.dunkel .zeile svg:not(.sym-rv){color:#f5f5f5}
.ab{width:6px}.fehlt{width:32px;text-align:center;opacity:.5}
.lupe{width:64px;height:64px;image-rendering:pixelated;flex:none}
h1{margin:0;padding:10px 14px;font-size:18px;background:#222933;color:#fff}.text{padding:6px 14px 10px;background:#f7f7f7;color:#222;margin:0 0 12px;width:1472px}
.menue{padding:8px 14px 14px;margin:0 0 12px;background:#e9ebee;color:#222;width:1472px}.menue img{display:block;margin:2px 0 8px;max-width:100%;border:1px solid #bbb}
.menue h3{margin:10px 0 0;font-size:13px}
`;

const lupenSkript = `
window.__lupen = 0;
const cs = [...document.querySelectorAll('canvas[data-svg]')]; window.__lupenSoll = cs.length;
for (const c of cs) { const img = new Image(); img.onload = () => { c.getContext('2d').drawImage(img, 0, 0, Number(c.dataset.g), Number(c.dataset.g)); window.__lupen++; }; img.onerror = () => { window.__lupen++; }; img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(c.dataset.svg); }
`;

function seitenDatei(inhalt) {
  return `<!doctype html><html lang="de"><head><meta charset="utf-8"><title>Kontaktbogen Symbole im Revit-Stil</title><style>${SEITENCSS}</style></head><body>${inhalt}<script>${lupenSkript}</script></body></html>`;
}

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgekb-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1920,1000', '--no-first-run', 'about:blank'], { stdio: 'ignore' });
let server = null;
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); proc.kill(); process.exit(2); }
const ws = new WebSocket(ziel.webSocketDebuggerUrl);
await new Promise((r) => (ws.onopen = r));
let nr = 0; const offen = new Map(); const seitenFehler = [];
ws.onmessage = (m) => {
  const d = JSON.parse(m.data);
  if (d.id && offen.has(d.id)) { offen.get(d.id)(d); offen.delete(d.id); return; }
  if (d.method === 'Runtime.exceptionThrown') seitenFehler.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
};
const send = (method, params = {}) => new Promise((res, rej) => { const id = ++nr; offen.set(id, (d) => (d.error ? rej(new Error(JSON.stringify(d.error))) : res(d.result))); ws.send(JSON.stringify({ id, method, params })); });
async function ausw(expr) {
  const r = await send('Runtime.evaluate', { expression: `(async()=>{${expr}})()`, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
  return r.result.value;
}
await send('Runtime.enable'); await send('Page.enable');
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };
const speichern = (name, data) => { const f = path.join(AUS, name); writeFileSync(f, Buffer.from(data, 'base64')); return f; };

const ergebnis = { raster: [], menue: [] };
try {
  // ================================================================ Raster
  const rasterHtml = [];
  for (const design of ['hell', 'dunkel']) for (let i = 0; i < seiten.length; i++) rasterHtml.push(seitenHtml(design, i));
  const rasterDatei = path.join(tmpdir(), 'HLKS_CAD', 'kb_raster.html');
  mkdirSync(path.dirname(rasterDatei), { recursive: true });
  writeFileSync(rasterDatei, seitenDatei(rasterHtml.join('')), 'utf8');
  await send('Emulation.setDeviceMetricsOverride', { width: 1500, height: 1000, deviceScaleFactor: 1, mobile: false });
  await send('Page.navigate', { url: pathToFileURL(rasterDatei).href });
  await warteBis(() => ausw(`return document.readyState === 'complete' && window.__lupen >= window.__lupenSoll;`), 'Raster geladen', 30000);
  await warte(300);
  for (const design of ['hell', 'dunkel']) {
    for (let i = 0; i < seiten.length; i++) {
      const r = await ausw(`const e = document.getElementById('s-${design}-${i + 1}'); const b = e.getBoundingClientRect(); return { x: b.left + scrollX, y: b.top + scrollY, width: b.width, height: b.height };`);
      const bild = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true, clip: { ...r, scale: 1 } });
      ergebnis.raster.push(speichern(`symbole-${design}-${i + 1}.png`, bild.data));
    }
  }
  console.log(`Raster: ${ergebnis.raster.length} Bilder, ${namen.length} Symbole (${namen.length - fehlend.length} mit Revit-Fassung)`);

  // ================================================================ Menüband
  if (!ohneMenue) {
    if (!existsSync(path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'))) throw new Error(`Build fehlt: ${BUILD} (zuerst: HLKS_BUILD setzen und dotnet build src\\Hlks.Web src\\Hlks.Mcp)`);
    spawnSync('powershell', ['-NoProfile', '-Command', `$c = Get-NetTCPConnection -State Listen -LocalPort ${PORT} -ErrorAction SilentlyContinue | Select-Object -First 1; if ($c) { Stop-Process -Id $c.OwningProcess -Force }`]);
    rmSync(WURZEL, { recursive: true, force: true });
    const ABLAGE = path.join(WURZEL, 'ablage');
    for (const d of ['ablage', 'bib', 'aus', 'zuletzt', 'benutzer', 'anleitung', 'anleitung_pruefung']) mkdirSync(path.join(WURZEL, d), { recursive: true });
    const projektOrdner = path.join(ABLAGE, 'Test_KB_Weg 1', 'c_Pläne und Modelle', 'e_KICAD');
    const MCP = path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll');
    const r0 = spawnSync(dotnet, [MCP, '--beispiel', projektOrdner], { encoding: 'utf8' });
    if (r0.status !== 0) throw new Error('Beispielprojekt nicht angelegt ' + r0.stdout + r0.stderr);
    cpSync(WWW, path.join(WURZEL, 'wwwroot'), { recursive: true });   // Kopie: der Dateiwächter des Servers stürzt ab, wenn im Quellordner Dateien umbenannt werden
    const env = { ...process.env, HLKS_WEBROOT: path.join(WURZEL, 'wwwroot'), HLKS_TEST: '1', HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: path.join(WURZEL, 'bib'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(WURZEL, 'aus'), HLKS_ZULETZT: path.join(WURZEL, 'zuletzt'), HLKS_BENUTZER: path.join(WURZEL, 'benutzer'), HLKS_ANLEITUNG: path.join(WURZEL, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(WURZEL, 'anleitung_pruefung') };
    server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'KB', '--geraet', 'kb-test'], { env, stdio: 'ignore' });
    const SERVER = `http://localhost:${PORT}`;
    for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }

    await send('DOM.enable');
    await send('Emulation.setDeviceMetricsOverride', { width: 1920, height: 1000, deviceScaleFactor: 1, mobile: false });
    await send('Page.addScriptToEvaluateOnNewDocument', { source: `try { if (!localStorage.getItem('hlks.design')) localStorage.setItem('hlks.design', JSON.stringify('dunkel')); } catch (e) {}` });
    await send('Page.navigate', { url: `${SERVER}/` });
    await warteBis(() => ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && !!document.querySelector('.mb-tab');`), 'App gestartet', 30000);
    await warte(900);
    const REGISTER = ['Ändern', 'Architektur', 'Gebäudetechnik', 'Ansicht', 'Koordination'];
    const kurz = (r) => r.toLowerCase().replace('ä', 'ae').replace('ü', 'ue');
    for (const design of ['dunkel', 'hell']) {
      await ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(design)}, false); return 1;`);
      for (const stil of ['strich', 'revit']) {
        await ausw(`const m = await import('/js/oberflaeche/symbolstil-umschalten.js'); await m.symbolstilWaehlen(${JSON.stringify(stil)}, false); return 1;`);
        await warte(300);
        for (const reg of REGISTER) {
          await ausw(`const t = [...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(reg)}); if (!t) throw new Error('Register fehlt: ${reg}'); t.click(); return 1;`);
          await warte(350);
          const unten = await ausw(`const r = document.getElementById('menueband').getBoundingClientRect(); return Math.ceil(r.bottom) + 4;`);
          const bild = await send('Page.captureScreenshot', { format: 'png', clip: { x: 0, y: 0, width: 1920, height: unten, scale: 1 } });
          ergebnis.menue.push({ register: reg, stil: stil === 'strich' ? 'alt' : 'neu', design, datei: path.basename(speichern(`menue-${kurz(reg)}-${stil === 'strich' ? 'alt' : 'neu'}-${design}.png`, bild.data)) });
        }
      }
    }
    await ausw(`const m = await import('/js/oberflaeche/symbolstil-umschalten.js'); await m.symbolstilWaehlen('strich', false); return 1;`);
    console.log(`Menüband: ${ergebnis.menue.length} Bilder`);
  }

  // ================================================================ HTML
  const teile = [`<h1>Symbole im Revit-Stil (Teil S) – Kontaktbogen zur Freigabe</h1><div class="text"><b>Eigene Zeichnungen</b> in der Bildsprache von Revit (keine Grafiken von Autodesk). Je Symbol: alt (Strich, heutige Vorgabe) neben neu (Revit-Stil), 32 und 16 px, hell und dunkel; ganz rechts die 16-px-Fassung vierfach vergrössert. Stand: ${new Date().toLocaleString('de-CH')}. ${namen.length} Symbole, davon ${namen.length - fehlend.length} mit Revit-Fassung.${fehlend.length ? ` Ohne Revit-Fassung (Strich bleibt): ${fehlend.join(', ')}.` : ''}</div>`];
  if (ergebnis.menue.length) {
    for (const design of ['dunkel', 'hell']) {
      let h = `<section class="menue"><h2>Menüband bei 1920 px – ${design === 'hell' ? 'helles' : 'dunkles'} Design</h2>`;
      for (const reg of ['Ändern', 'Architektur', 'Gebäudetechnik', 'Ansicht', 'Koordination']) {
        for (const stil of ['alt', 'neu']) {
          const b = ergebnis.menue.find((m) => m.register === reg && m.stil === stil && m.design === design);
          if (b) h += `<h3>${reg} – ${stil === 'alt' ? 'alt (Strich)' : 'neu (Revit-Stil)'}</h3><img src="${b.datei}" alt="${reg} ${stil}">`;
        }
      }
      teile.push(h + '</section>');
    }
  }
  teile.push(...rasterHtml);
  writeFileSync(path.join(AUS, 'kontaktbogen.html'), seitenDatei(teile.join('')), 'utf8');
  console.log(`Ausgabe: ${AUS}`);
  console.log(`  ${path.join(AUS, 'kontaktbogen.html')}`);
} catch (e) {
  console.error('Abbruch:', e.stack ?? e);
  process.exitCode = 1;
} finally {
  try { ws.close(); } catch { /* egal */ }
  proc.kill(); server?.kill();
  if (seitenFehler.length) console.log('Fehler in der Seite:', seitenFehler.slice(0, 5));
  setTimeout(() => process.exit(process.exitCode ?? 0), 300);
}
