// Gemeinsame Hilfen der Browser-Reihen «freie Grösse» und «Abwasser-Serie» (freie-groesse.browser.mjs, abwasser-serie.browser.mjs): eigener Server mit frischem
// Beispielprojekt (alle Orte unter %TEMP%), Edge headless über CDP, echte Mausklicks und Tastatur, Aufzeichnung der Abfragen der Oberfläche (damit sich prüfen lässt,
// dass die langsame Abfrage leitungsserie nie ungefragt kommt). Nicht Teil von «node --test». Keine Fachlogik und keine Normwerte; alle Testfamilien sind FIKTIV.
//
// Umgebung (alle Werte können gesetzt werden, die Vorgaben stehen im Aufruf):
//   HLKS_BUILD bzw. HLKS_BAU  Build-Ordner (Standard: %USERPROFILE%\HLKS_CAD_build)
//   HLKS_KATALOG              Katalogordner der Arbeitskopie (Vorgabe: <Arbeitskopie>\kataloge)
//   HLKS_WEBROOT              Oberfläche der Arbeitskopie (Vorgabe: <Arbeitskopie>\src\Hlks.Web\wwwroot)
//   HLKS_TEST_SERVER          vorhandener Server mit frischem Beispielprojekt (statt eigenem)
//   SHOTS                     Ordner für die Bilder

import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const hier = path.dirname(fileURLToPath(import.meta.url));
export const REPO = path.resolve(hier, '../../../../..');
export const FAMILIEN = path.join(REPO, 'tests', 'daten', 'familien').replace(/\\/g, '/');
const VERBOTENE_PORTS = /:(5013|5098|5130|5131|5149|5150|5151|5152|5153|5154|5155)\b/;

/**
 * Startet Server und Browser.
 * o: { kurz (Kennung für Ordner und Bilder), port (ab 5346), cdpPort, portVar (Umgebungsvariable für den Port) }
 */
export async function starten(o) {
  const PORT = Number(process.env[o.portVar] ?? o.port);
  const EIGENER_SERVER = !process.env.HLKS_TEST_SERVER;
  const SERVER = process.env.HLKS_TEST_SERVER ?? `http://localhost:${PORT}`;
  if (VERBOTENE_PORTS.test(SERVER)) { console.error('Dieser Test läuft nie gegen 5013, 5098, 5130, 5131 oder 5149–5155.'); process.exit(2); }
  const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? o.cdpPort);
  const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
  const dotnet = process.env.HLKS_DOTNET ?? 'C:\\Program Files\\dotnet\\dotnet.exe';
  const BAU = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_build');
  const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), `hlks_${o.kurz}`);
  mkdirSync(SHOTS, { recursive: true });
  const warte = (ms) => new Promise((r) => setTimeout(r, ms));
  const arbeit = mkdtempSync(path.join(tmpdir(), `hlks_${o.kurz}_`));
  // Alle Orte, die der Server sonst beim Benutzer sucht, liegen unter %TEMP%; Katalog und Oberfläche sind die der Arbeitskopie
  const ENV = {
    HLKS_TEST: '1', HLKS_BENUTZER: path.join(arbeit, 'benutzer'), HLKS_ANLEITUNG: path.join(arbeit, 'anleitung'), HLKS_ANLEITUNG_PRUEFUNG: path.join(arbeit, 'anleitung_pruefung'),
    HLKS_ZULETZT: path.join(arbeit, 'zuletzt'), HLKS_FAMILIENBIBLIOTHEK: path.join(arbeit, 'familienbibliothek'), HLKS_BIBLIOTHEK_AUSSTEHEND: path.join(arbeit, 'ausstehend'), HLKS_PROJEKTABLAGE: path.join(arbeit, 'projektablage'),
    HLKS_KATALOG: process.env.HLKS_KATALOG ?? path.join(REPO, 'kataloge'), HLKS_WEBROOT: process.env.HLKS_WEBROOT ?? path.join(REPO, 'src', 'Hlks.Web', 'wwwroot'),
  };

  const h = { SERVER, SHOTS, warte, ok: 0, schlecht: 0, PORT };
  h.pruef = (bed, t, info = '') => { if (bed) { h.ok++; console.log('  ok   ', t); } else { h.schlecht++; console.log('  FEHL ', t, info); } };
  h.abschnitt = (t) => console.log('\n== ' + t);

  // ------------------------------------------------------------------ Server
  let serverProzess = null, proc = null;
  const laufen = (exe, args) => new Promise((res, rej) => {
    const p = spawn(exe, args, { env: { ...process.env, ...ENV }, stdio: ['ignore', 'pipe', 'pipe'] });
    let aus = ''; p.stdout.on('data', (d) => { aus += d; }); p.stderr.on('data', (d) => { aus += d; });
    p.on('exit', (c) => (c === 0 ? res(aus) : rej(new Error(`${exe} ${args.join(' ')} → ${c}\n${aus}`))));
  });
  if (EIGENER_SERVER) {
    const projekt = path.join(arbeit, 'c_Plaene und Modelle', 'e_KICAD');
    await laufen(dotnet, [path.join(BAU, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll'), '--beispiel', projekt]);
    serverProzess = spawn(dotnet, [path.join(BAU, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projekt, '--akteur', 'T', '--geraet', o.kurz], { stdio: 'ignore', env: { ...process.env, ...ENV } });
    for (let i = 0; i < 60; i++) { try { const r = await fetch(`${SERVER}/api/status`); if (r.ok && (await r.json()).offen) break; } catch { /* noch nicht */ } await warte(500); }
  }
  const kernPost = async (pfad, body) => (await fetch(`${SERVER}${pfad}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();
  h.kern = (art, name, body, trocken = false) => kernPost(`/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, body);
  h.modell = async () => { const m = await (await fetch(`${SERVER}/api/modell`)).json(); return m.elemente ?? m; };
  h.derKlasse = async (k) => (await h.modell()).filter((e) => e.klasse === k);
  h.aufraeumen = async (code) => {
    try { proc?.kill(); } catch { /* egal */ }
    try { serverProzess?.kill(); } catch { /* egal */ }       // nur der eigene Prozess (PID), nie über den Namen
    await warte(800);
    try { rmSync(arbeit, { recursive: true, force: true }); } catch { /* der Server hält eine Datei noch offen */ }
    process.exit(code);
  };

  // ------------------------------------------------------------------ Edge
  const profil = mkdtempSync(path.join(tmpdir(), `edge_${o.kurz}-`));
  proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,950', '--no-first-run', '--enable-unsafe-swiftshader', 'about:blank'], { stdio: 'ignore' });
  let ziel = null;
  for (let i = 0; i < 50; i++) {
    try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
  }
  if (!ziel) { console.error('Edge nicht erreichbar'); await h.aufraeumen(2); }
  const ws = new WebSocket(ziel.webSocketDebuggerUrl);
  await new Promise((r) => (ws.onopen = r));
  let nr = 0; const offen = new Map(); h.fehler = []; h.konsole = [];
  ws.onmessage = (m) => {
    const d = JSON.parse(m.data);
    if (d.id && offen.has(d.id)) { offen.get(d.id)(d); offen.delete(d.id); return; }
    if (d.method === 'Runtime.exceptionThrown') h.fehler.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
    if (d.method === 'Runtime.consoleAPICalled' && d.params.type === 'error') h.konsole.push(d.params.args.map((a) => a.value ?? a.description).join(' '));
  };
  const send = (method, params = {}) => new Promise((res, rej) => { const id = ++nr; offen.set(id, (d) => (d.error ? rej(new Error(JSON.stringify(d.error))) : res(d.result))); ws.send(JSON.stringify({ id, method, params })); });
  h.send = send;
  const ausw = async (expr) => {
    const r = await send('Runtime.evaluate', { expression: `(async()=>{${expr}})()`, awaitPromise: true, returnByValue: true });
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
    return r.result.value;
  };
  h.ausw = ausw;
  await send('Runtime.enable'); await send('Page.enable'); await send('DOM.enable');
  await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 950, deviceScaleFactor: 1, mobile: false });
  // Aufzeichnung aller Abfragen und Befehle der Oberfläche (vor dem Laden der Seite): window.__aufruf = [{ art, name, t, ende, fehler }]
  await send('Page.addScriptToEvaluateOnNewDocument', { source: `(() => { window.__aufruf = []; const f = window.fetch.bind(window); window.fetch = (u, opt) => { const m = /\\/api\\/(abfrage|befehl)\\/([^?]+)/.exec(String(u)); if (!m) return f(u, opt); const e = { art: m[1], name: decodeURIComponent(m[2]), t: Date.now(), ende: null, fehler: null }; window.__aufruf.push(e); return f(u, opt).then((r) => { e.ende = Date.now(); return r; }, (x) => { e.ende = Date.now(); e.fehler = String(x && x.name || x); throw x; }); }; })();` });
  await send('Page.navigate', { url: `${SERVER}/` });

  h.aufrufe = (name) => ausw(`return (window.__aufruf ?? []).filter((e) => e.name === ${JSON.stringify(name)}).map((e) => ({ art: e.art, name: e.name, offen: e.ende === null, fehler: e.fehler }));`);
  h.shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, `${o.kurz}_${name}.png`); writeFileSync(f, Buffer.from(r.data, 'base64')); console.log('   Bild:', f); };
  h.warteBis = async (fn, t, ms = 20000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(120); } throw new Error('Zeitüberschreitung: ' + t); };

  // ------------------------------------------------------------------ echte Mausklicks und Tastatur
  const INST = `const f = await import('/js/fenster.js'); const inst = f.aktivePlanansicht();`;
  h.INST = INST;
  const bs = (p) => ausw(`${INST} return inst.zuBildschirm(${JSON.stringify(p)});`);
  h.bs = bs;
  const maus = async (typ, x, y, extra = {}) => { await send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'none', ...extra }); };
  h.maus = maus;
  h.bewege = async (p, mod = 0) => { const b = await bs(p); await maus('mouseMoved', b.x, b.y, { modifiers: mod }); await warte(60); return b; };
  h.klickM = async (p, { mod = 0, taste = 'left', n = 1 } = {}) => {
    const b = await bs(p);
    await maus('mouseMoved', b.x, b.y, { modifiers: mod }); await warte(60);
    for (let i = 1; i <= n; i++) {
      await send('Input.dispatchMouseEvent', { type: 'mousePressed', x: b.x, y: b.y, button: taste, buttons: taste === 'left' ? 1 : 2, clickCount: i, modifiers: mod });
      await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: b.x, y: b.y, button: taste, buttons: 0, clickCount: i, modifiers: mod });
      await warte(25);
    }
    await warte(200);
    return b;
  };
  const klickAn = async (x, y, { taste = 'left', n = 1 } = {}) => {
    await maus('mouseMoved', x, y); await warte(30);
    for (let i = 1; i <= n; i++) {
      await send('Input.dispatchMouseEvent', { type: 'mousePressed', x, y, button: taste, buttons: taste === 'left' ? 1 : 2, clickCount: i });
      await send('Input.dispatchMouseEvent', { type: 'mouseReleased', x, y, button: taste, buttons: 0, clickCount: i });
    }
    await warte(200);
  };
  const mitte = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); if (!e) return null; e.scrollIntoView({ block: 'nearest', inline: 'nearest' }); const r = e.getBoundingClientRect(); return r.width === 0 && r.height === 0 ? null : { x: r.left + Math.min(r.width / 2, 40), y: r.top + r.height / 2 };`);
  /** Klick auf ein Element der Oberfläche (Mitte bzw. linkes Drittel), prüft, dass nichts davor liegt. */
  h.klickS = async (sel, opt = {}) => {
    const p = await mitte(sel);
    if (!p) throw new Error('Element nicht sichtbar: ' + sel);
    const oben = await ausw(`const e = document.querySelector(${JSON.stringify(sel)}); const t = document.elementFromPoint(${p.x}, ${p.y}); return !!t && (e === t || e.contains(t) || t.contains(e) || (e.closest('label') && e.closest('label').contains(t)));`);
    if (!oben) throw new Error('Element ist verdeckt: ' + sel);
    await klickAn(p.x, p.y, opt);
  };
  const CODES = { Enter: ['Enter', 13], Escape: ['Escape', 27], Backspace: ['Backspace', 8], ' ': ['Space', 32], Tab: ['Tab', 9], z: ['KeyZ', 90], y: ['KeyY', 89] };
  h.taste = async (key, mod = 0) => {
    const [code, vk] = CODES[key] ?? (key.length === 1 && /[a-z]/i.test(key) ? [`Key${key.toUpperCase()}`, key.toUpperCase().charCodeAt(0)] : [`Digit${key}`, key.charCodeAt(0)]);
    const text = mod === 0 && (key.length === 1) ? key : mod === 0 && key === 'Enter' ? '\r' : undefined;
    await send('Input.dispatchKeyEvent', { type: text ? 'keyDown' : 'rawKeyDown', key, code, windowsVirtualKeyCode: vk, modifiers: mod, text });
    await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code, windowsVirtualKeyCode: vk, modifiers: mod });
    await warte(80);
  };
  h.tippeTasten = async (s) => { for (const c of s) await h.taste(c); await warte(80); };
  h.zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);
  h.gibt = (sel) => ausw(`return !!document.querySelector(${JSON.stringify(sel)});`);
  h.dom = (sel) => ausw(`return document.querySelector(${JSON.stringify(sel)})?.innerText ?? null;`);
  h.anzahl = (sel) => ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`);
  h.wert = (sel) => ausw(`const e = document.querySelector(${JSON.stringify(sel)}); return e ? (e.type === 'checkbox' ? e.checked : e.value) : null;`);
  h.meldungen = () => ausw(`return document.querySelector('#meldungen')?.innerText ?? '';`);
  h.vorschauText = () => ausw(`const l = document.querySelector('.lt-leiste'); return l && !l.hidden ? l.innerText : null;`);
  h.vorschauSichtbar = () => ausw(`const l = document.querySelector('.lt-leiste'); return !!l && !l.hidden;`);
  h.statusText = () => ausw(`return document.querySelector('#statusleiste')?.innerText ?? '';`);
  h.werkzeug = () => ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`);
  h.werkTexte = () => ausw(`return [...document.querySelectorAll('.gr-werkzeug text')].map((t) => t.textContent).join(' | ');`);
  h.alleSchliessen = async () => { for (let i = 0; i < 6 && (await ausw(`return document.querySelectorAll('#dialogschicht .dlg').length;`)) > 0; i++) { await h.taste('Escape'); await warte(150); } };
  // Felder der Optionsleiste wie ein Benutzer setzen: Wert, input, change, Fokus weg
  h.feldWert = (feld) => ausw(`return document.querySelector('#optionsleiste [data-feld="${feld}"]')?.value ?? null;`);
  h.feldSetzen = (feld, wert) => ausw(`const e = document.querySelector('#optionsleiste [data-feld="${feld}"]'); if (!e) throw new Error('Feld fehlt: ${feld}'); e.focus(); if (e.tagName === 'SELECT') Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(e, ${JSON.stringify(String(wert))}); else e.value = ${JSON.stringify(String(wert))}; e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); e.blur(); return e.value;`);
  /** Feld der Palette setzen wie ein Benutzer. */
  h.paletteSetzen = (ziel, wert) => ausw(`const e = document.querySelector('[data-ziel="${ziel}"]'); if (!e) throw new Error('Feld fehlt: ${ziel}'); e.focus(); if (e.tagName === 'SELECT') Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value').set.call(e, ${JSON.stringify(String(wert))}); else e.value = ${JSON.stringify(String(wert))}; e.dispatchEvent(new Event('input', { bubbles: true })); e.dispatchEvent(new Event('change', { bubbles: true })); return e.value;`);
  h.dlgKnopf = async (t) => {
    const dlg = '#dialogschicht .dlg:last-child';
    const id = await ausw(`const b = [...document.querySelectorAll('${dlg} .dlg-knopf')].find(b => b.textContent.trim() === ${JSON.stringify(t)}); if (!b) return null; b.setAttribute('data-test', 'k'); return true;`);
    if (!id) throw new Error('Dialogknopf fehlt: ' + t);
    await h.klickS(`${dlg} .dlg-knopf[data-test="k"]`);
    await ausw(`document.querySelectorAll('[data-test="k"]').forEach(e => e.removeAttribute('data-test')); return 1;`);
  };
  h.dlgText = () => ausw(`return document.querySelector('#dialogschicht .dlg:last-child')?.innerText ?? null;`);
  h.lage = (kz) => h.zustandDaten(`z.element('${kz}')?.geometrie?.punkte`);

  /** Grundriss EG öffnen und den Ausschnitt des Beispielprojekts einstellen. */
  h.grundriss = async () => {
    await h.warteBis(async () => (await h.zustandDaten(`z.zustand.elemente.size > 50`)), 'App gestartet', 60000);
    await warte(1200);
    await ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen('AN-GR-EG'); return 1;`);
    await h.warteBis(() => ausw(`${INST} return !!inst && inst.art === 'grundriss';`), 'Grundriss EG');
    h.BOX_ALLES = { minx: 12500, miny: -500, maxx: 22500, maxy: 11000 };
    await h.zoomAuf(h.BOX_ALLES);
  };
  h.zoomAuf = async (box) => { await ausw(`${INST} inst.zoomAuf(${JSON.stringify(box)}); return 1;`); await warte(300); };
  /** Familien über den Kern laden (Dateien aus tests/daten/familien). */
  h.laden = async (...namen) => {
    for (const n of namen) {
      const r = await h.kern('befehl', 'familie_laden', { datei: `${FAMILIEN}/${n}.familie.json` });
      if (!r.angenommen) throw new Error(`${n}: ${r.fehler}`);
    }
  };
  /** Wartet, bis die Familienliste der Oberfläche eine Familie dieser Kategorie kennt. */
  h.familienDa = (kategorie, mindestens = 1) => h.warteBis(() => ausw(`const d = await import('/js/familien/daten.js'); await d.familienLaden(); return d.familienListe().filter((f) => f.kategorie === ${JSON.stringify(kategorie)}).length >= ${mindestens};`), `Familienliste (${kategorie})`);

  h.ende = async () => {
    h.abschnitt('Konsole');
    h.pruef(h.fehler.length === 0, 'keine Ausnahmen in der Seite', h.fehler.join(' | '));
    h.pruef(h.konsole.filter((k) => !/Failed to load resource|ERR_/.test(k)).length === 0, 'keine Konsolenfehler', h.konsole.join(' | '));
    console.log(`\n${h.ok} bestanden, ${h.schlecht} fehlgeschlagen`);
    await h.aufraeumen(h.schlecht > 0 ? 1 : 0);
  };
  return h;
}
