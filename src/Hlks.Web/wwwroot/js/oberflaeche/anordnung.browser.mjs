// Browser-Integrationstest der frei anordenbaren Ansichten und des eigenen Fensters (Teil W2, entwurf/fenster-revit.md): Edge headless über CDP,
// echte Mausereignisse. Nicht Teil von «node --test». Der Test baut seine Umgebung selbst auf (Beispielprojekt, Server) und räumt nicht weg:
//   %TEMP%\HLKS_CAD\w2_test\ablage\Test_W2 1\c_Pläne und Modelle\e_KICAD   Beispielprojekt (Mcp --beispiel)
//   %TEMP%\HLKS_CAD\w2_test\bib, \aus, \zuletzt                            Familienbibliothek, ausstehende Familien, Liste «zuletzt»
// Die Oberfläche wird aus einer Kopie ausgeliefert (HLKS_WEBROOT = %TEMP%\HLKS_CAD\w2_test\wwwroot).
// Der Server läuft mit HLKS_TEST=1 und eigenen Ordnern unter %TEMP%: nie auf W:.
// Aufruf:  node anordnung.browser.mjs      (Port 5178, HLKS_W2_PORT; Edge-Port 9510, HLKS_CDP_PORT; Build HLKS_BAU bzw. HLKS_BUILD)
// Umgebung: SHOTS=<Ordner für Bilder> (Vorgabe %TEMP%\HLKS_CAD), HLKS_EDGE
// Prüft: Gruppen mit eigener Reiterleiste; Reiter per Maus an den Rand ziehen (Andockziele sichtbar), Trenner ziehen, Reiter in eine Gruppe
// ziehen; drei Gruppen; «Kacheln» und «Als Reiter»; Neuladen stellt alles wieder her; Werkzeug-Ansicht (aktivePlanansicht) je Gruppe; Reiter «▾ n weitere»
// je Gruppe; eigenes Fenster (Kontextmenü, Ziehen aus dem Fenster): zweites Fenster «nur Ansicht», Live-Abgleich von Auswahl und Änderungen
// in beide Richtungen, Rückgabe beim Schliessen und mit dem Knopf, Neuladen des Hauptfensters mit lebendem Zweitfenster. Bilder: w2_*.png (hell und dunkel).
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, rmSync, cpSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const PORT = Number(process.env.HLKS_W2_PORT ?? 5178);
const SERVER = `http://localhost:${PORT}`;
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9510);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const dotnet = 'C:\\Program Files\\dotnet\\dotnet.exe';
const BUILD = process.env.HLKS_BAU ?? process.env.HLKS_BUILD ?? path.join(process.env.USERPROFILE, 'HLKS_CAD_build');
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'HLKS_CAD');
const WURZEL = path.join(tmpdir(), 'HLKS_CAD', PORT === 5178 ? 'w2_test' : `w2_test_${PORT}`);
const hier = path.dirname(fileURLToPath(import.meta.url));
mkdirSync(SHOTS, { recursive: true });

const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(100); } throw new Error('Zeitüberschreitung: ' + t); };

// ------------------------------------------------------------------ Umgebung aufbauen
if (!tmpdir().toLowerCase().includes('temp')) { console.error('Abbruch: %TEMP% sieht nicht nach einem Temp-Ordner aus.'); process.exit(2); }
spawnSync('powershell', ['-NoProfile', '-Command', `$c = Get-NetTCPConnection -State Listen -LocalPort ${PORT} -ErrorAction SilentlyContinue | Select-Object -First 1; if ($c) { Stop-Process -Id $c.OwningProcess -Force }`]);
rmSync(WURZEL, { recursive: true, force: true });
const ABLAGE = path.join(WURZEL, 'ablage'), BIB = path.join(WURZEL, 'bib'), AUS = path.join(WURZEL, 'aus'), ZULETZT = path.join(WURZEL, 'zuletzt');
for (const d of [ABLAGE, BIB, AUS, ZULETZT]) mkdirSync(d, { recursive: true });
const projektOrdner = path.join(ABLAGE, 'Test_W2 1', 'c_Pläne und Modelle', 'e_KICAD');
const MCP = path.join(BUILD, 'Hlks.Mcp', 'bin', 'Debug', 'net10.0', 'Hlks.Mcp.dll');
{
  const r = spawnSync(dotnet, [MCP, '--beispiel', projektOrdner], { encoding: 'utf8' });
  if (r.status !== 0) { console.error('Beispielprojekt nicht angelegt', r.stdout, r.stderr); process.exit(2); }
}
cpSync(path.resolve(hier, '../..'), path.join(WURZEL, 'wwwroot'), { recursive: true });
const env = { ...process.env, HLKS_WEBROOT: path.join(WURZEL, 'wwwroot'), HLKS_TEST: '1', HLKS_PROJEKTABLAGE: ABLAGE, HLKS_FAMILIENBIBLIOTHEK: BIB, HLKS_BIBLIOTHEK_AUSSTEHEND: AUS, HLKS_ZULETZT: ZULETZT };
const server = spawn(dotnet, [path.join(BUILD, 'Hlks.Web', 'bin', 'Debug', 'net10.0', 'Hlks.Web.dll'), '--port', String(PORT), '--projekt', projektOrdner, '--akteur', 'W2', '--geraet', 'w2-test'], { env, stdio: 'ignore' });
for (let i = 0; i < 80; i++) { try { if ((await fetch(`${SERVER}/api/status`)).ok) break; } catch { /* warten */ } await warte(250); }
const kern = async (art, name, body) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};

// ------------------------------------------------------------------ Browser
const profil = mkdtempSync(path.join(tmpdir(), 'edgew2-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,900', '--no-first-run', 'about:blank'], { stdio: 'ignore' });
const beenden = (code) => { try { proc.kill(); } catch { /* egal */ } try { server.kill(); } catch { /* egal */ } process.exit(code); };

/** Eine Seite (Hauptfenster oder Zweitfenster) über CDP. */
class Seite {
  constructor(ziel, name) { this.ziel = ziel; this.name = name; this.nr = 0; this.offen = new Map(); this.fehler = []; this.konsole = []; }
  async verbinden() {
    this.ws = new WebSocket(this.ziel.webSocketDebuggerUrl);
    await new Promise((r) => (this.ws.onopen = r));
    this.ws.onmessage = (m) => {
      const d = JSON.parse(m.data);
      if (d.id && this.offen.has(d.id)) { this.offen.get(d.id)(d); this.offen.delete(d.id); return; }
      if (d.method === 'Runtime.exceptionThrown') this.fehler.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
      if (d.method === 'Runtime.consoleAPICalled' && d.params.type === 'error') this.konsole.push(d.params.args.map((a) => a.value ?? a.description).join(' '));
    };
    await this.send('Runtime.enable'); await this.send('Page.enable');
    return this;
  }
  send(method, params = {}) { return new Promise((res, rej) => { const id = ++this.nr; this.offen.set(id, (d) => (d.error ? rej(new Error(JSON.stringify(d.error))) : res(d.result))); this.ws.send(JSON.stringify({ id, method, params })); }); }
  async ausw(expr) {
    const r = await this.send('Runtime.evaluate', { expression: `(async()=>{${expr}})()`, awaitPromise: true, returnByValue: true, userGesture: true });   // wie ein Klick: window.open wird nicht als Pop-up gesperrt
    if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
    return r.result.value;
  }
  async groesse(w, h) { await this.send('Emulation.setDeviceMetricsOverride', { width: w, height: h, deviceScaleFactor: 1, mobile: false }); await warte(300); }
  async shot(name) { const r = await this.send('Page.captureScreenshot', { format: 'png' }); const f = path.join(SHOTS, name + '.png'); writeFileSync(f, Buffer.from(r.data, 'base64')); return f; }
  maus(type, x, y, extra = {}) { return this.send('Input.dispatchMouseEvent', { type, x, y, button: 'left', buttons: type === 'mouseReleased' ? 0 : 1, clickCount: 1, ...extra }); }
  async klickAn(p, opt = {}) {
    await this.send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: p.x, y: p.y });
    await this.send('Input.dispatchMouseEvent', { type: 'mousePressed', x: p.x, y: p.y, button: opt.knopf ?? 'left', buttons: opt.knopf === 'right' ? 2 : 1, clickCount: 1 });
    await this.send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: p.x, y: p.y, button: opt.knopf ?? 'left', buttons: 0, clickCount: 1 });
    await warte(opt.warten ?? 250);
  }
  /** Ziehen von p nach q in Schritten; zwischen/vor dem Loslassen kann `mitte(seite)` Zwischenstände prüfen. */
  async ziehen(p, q, { schritte = 12, mitte = null, loslassen = true } = {}) {
    await this.send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: p.x, y: p.y });
    await this.send('Input.dispatchMouseEvent', { type: 'mousePressed', x: p.x, y: p.y, button: 'left', buttons: 1, clickCount: 1 });
    for (let i = 1; i <= schritte; i++) {
      await this.send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: p.x + ((q.x - p.x) * i) / schritte, y: p.y + ((q.y - p.y) * i) / schritte, button: 'left', buttons: 1 });
      await warte(15);
    }
    await warte(150);
    if (mitte) await mitte(this);
    if (loslassen) { await this.send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: q.x, y: q.y, button: 'left', buttons: 0, clickCount: 1 }); await warte(350); }
  }
  mitteVon(selektor, index = 0) { return this.ausw(`const e = document.querySelectorAll(${JSON.stringify(selektor)})[${index}]; if (!e) return null; const r = e.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2, l: r.left, t: r.top, b: r.width, h: r.height };`); }
  anzahl(sel) { return this.ausw(`return document.querySelectorAll(${JSON.stringify(sel)}).length;`); }
  /** Daten der Anordnung und der Rechtecke aus fenster.js. */
  async daten() { return this.ausw(`const f = await import('/js/fenster.js'); return { a: f.anordnungDaten(), geo: f.rechtecke(), modus: f.fenstermodus(), offen: f.alle().map(o => o.id) };`); }
  /** Kennzeichen → Id. */
  id(kz) { return this.ausw(`const z = await import('/js/zustand.js'); return z.element(${JSON.stringify(kz)})?.id ?? null;`); }
  /** Reiter mit dem Titel (Teilstring). */
  reiterMitte(titel) { return this.ausw(`const r = [...document.querySelectorAll('.reiterleiste > .reiter')].find(x => x.querySelector('.titel').textContent.includes(${JSON.stringify(titel)}) && getComputedStyle(x).display !== 'none'); if (!r) return null; const b = r.getBoundingClientRect(); return { x: b.left + b.width / 2, y: b.top + b.height / 2 };`); }
  async designSetzen(d) { await this.ausw(`const m = await import('/js/oberflaeche/design.js'); m.designSetzen(${JSON.stringify(d)}, false); return 1;`); await warte(400); }
}

let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent('about:blank')}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); beenden(2); }
const haupt = await new Seite(ziel, 'haupt').verbinden();
await haupt.groesse(1500, 860);
const gehe = async (seite, url, design = 'dunkel') => {
  await seite.send('Page.addScriptToEvaluateOnNewDocument', { source: `try { if (!localStorage.getItem('hlks.design')) localStorage.setItem('hlks.design', JSON.stringify('${design}')); } catch (e) {}` }).catch(() => {});
  await seite.send('Page.navigate', { url });
  await warteBis(() => seite.ausw(`const z = await import('/js/zustand.js'); return document.readyState === 'complete' && z.zustand.elemente.size > 50 && document.querySelectorAll('.reiterleiste > .reiter').length > 0;`), 'App gestartet', 40000);
  await warte(700);
};

try {
  // ---------------------------------------------------------------- Vorbereitung
  abschnitt('Vorbereitung: Schnitt anlegen, Fenster öffnen');
  await haupt.send('Page.navigate', { url: `${SERVER}/` });
  await warteBis(() => haupt.ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50;`), 'Modell geladen', 40000);
  const grundrisse = await haupt.ausw(`const z = await import('/js/zustand.js'); return z.derKlasse('Ansicht').filter(a => a.werte?.art === 'grundriss' && !a.werte?.vorlage).map(a => ({ kz: a.kennzeichen, name: a.name || a.kennzeichen, geschoss: a.geschoss })).sort((a, b) => a.kz.localeCompare(b.kz));`);
  pruef(grundrisse.length >= 2, `Beispielprojekt hat mehrere Grundrisse (${grundrisse.length})`);
  const GR1 = grundrisse[0].kz, GR2 = grundrisse[1].kz, NAME1 = grundrisse[0].name;
  const r = await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-SCHNITT-W2', name: 'Schnitt W2', geschoss: grundrisse[0].geschoss, eigenschaften: { art: 'schnitt', blick: 'rechts', tiefe: 10000, massstab: 100 }, geometrie: { art: 'linie', punkte: [[0, 4000], [12000, 4000]] } });
  pruef(r.angenommen === true, 'Schnitt «AN-SCHNITT-W2» angelegt', JSON.stringify(r));
  const SCHNITT = 'AN-SCHNITT-W2', D3 = 'AN-3D';
  await gehe(haupt, `${SERVER}/`);

  // ---------------------------------------------------------------- 1. Start: eine Gruppe, Reiter wie bisher
  abschnitt('1. Start: eine Gruppe mit Reitern wie bisher');
  let d = await haupt.daten();
  pruef(d.a.wurzel?.art === 'gruppe' && d.modus === 'reiter', 'Start: eine Gruppe, Modus «reiter»', JSON.stringify(d.a));
  pruef(await haupt.ausw(`return !!document.getElementById('ansichtsreiter') && document.querySelectorAll('#ansichtsreiter > .reiter').length >= 1 && !!document.querySelector('#ansichtsreiter .reiter.aktiv .titel');`), '#ansichtsreiter mit aktivem Reiter (wie bisher)');
  for (const kz of [GR2, SCHNITT, D3]) await haupt.ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(kz)}); return 1;`);
  await warteBis(() => haupt.ausw(`return !!document.querySelector('.kachel.aktiv .d3-leinwand');`), '3D gezeichnet', 30000);
  d = await haupt.daten();
  pruef(d.a.wurzel.art === 'gruppe' && d.a.wurzel.ansichten.length === 4, 'vier Ansichten als Reiter einer Gruppe', JSON.stringify(d.a));
  pruef(await haupt.ausw(`return document.querySelectorAll('.kachel:not(.versteckt)').length;`) === 1, 'genau eine Kachel sichtbar');
  pruef(await haupt.anzahl('.reiter-weitere') === 1 && await haupt.anzahl('.reiter-liste') === 1, 'mit einer Gruppe gibt es genau eine Leiste mit «weitere» (keine verwaiste zweite)');

  // ---------------------------------------------------------------- 2. Reiter an den Rand ziehen
  abschnitt('2. Reiter «Schnitt» an den rechten Rand ziehen (Andockziele)');
  const idSchnitt = await haupt.id(SCHNITT), idGr1 = await haupt.id(GR1), id3d = await haupt.id(D3), idGr2 = await haupt.id(GR2);
  // erst Grundriss aktivieren, damit der Schnitt im Hintergrund-Reiter ist
  await haupt.klickAn(await haupt.reiterMitte(NAME1));
  const flaeche = await haupt.mitteVon('#ansichtsflaeche');
  const schnittReiter = await haupt.reiterMitte('Schnitt W2');
  pruef(!!schnittReiter, 'Reiter «Schnitt W2» gefunden');
  let beimZiehen = null;
  const ziel1 = { x: flaeche.l + flaeche.b * 0.88, y: flaeche.t + flaeche.h * 0.5 };
  await haupt.ziehen(schnittReiter, ziel1, {
    mitte: async (s) => {
      beimZiehen = await s.ausw(`return { kreuz: document.querySelectorAll('.ag-kreuz .ag-lp').length, ziel: document.querySelectorAll('.ag-kreuz .ag-lp.ziel').length, vorschau: !document.querySelector('.ag-vorschau').hidden, schatten: !!document.querySelector('.ag-schatten'), rand: document.querySelectorAll('.ag-rand:not([hidden])').length, ortKlasse: document.querySelector('.ag-kreuz .ag-lp.ziel')?.className ?? null, vb: (() => { const v = document.querySelector('.ag-vorschau').getBoundingClientRect(); const f = document.getElementById('ansichtsflaeche').getBoundingClientRect(); return { links: v.left - f.left, b: v.width, fb: f.width }; })() };`);
      await s.shot('w2_andocken_dunkel');
    },
  });
  pruef(beimZiehen?.kreuz === 5 && beimZiehen.vorschau && beimZiehen.schatten, 'beim Ziehen: fünf Andockziele, Vorschau und Reiter-Schatten sichtbar', JSON.stringify(beimZiehen));
  pruef(beimZiehen?.ziel === 1 && /ag-lp-rechts/.test(beimZiehen.ortKlasse ?? ''), 'das Ziel «rechts» ist hervorgehoben', JSON.stringify(beimZiehen));
  pruef(beimZiehen?.rand === 3, 'drei Streifen am Rand der Fläche sichtbar', JSON.stringify(beimZiehen));
  pruef(beimZiehen && beimZiehen.vb.links > beimZiehen.vb.fb * 0.4 && beimZiehen.vb.b < beimZiehen.vb.fb * 0.6, 'die Vorschau zeigt die rechte Hälfte', JSON.stringify(beimZiehen?.vb));
  pruef((await haupt.anzahl('.ag-ueberlagerung')) === 0 && (await haupt.anzahl('.ag-schatten')) === 0, 'nach dem Loslassen sind Andockziele und Schatten weg');
  d = await haupt.daten();
  pruef(d.a.wurzel.art === 'teilung' && d.a.wurzel.richtung === 'nebeneinander' && d.geo.gruppen.length === 2 && d.modus === 'nebeneinander', 'zwei Gruppen nebeneinander, Modus «nebeneinander»', JSON.stringify(d.a));
  pruef(d.a.wurzel.kinder[1].ansichten.length === 1 && d.a.wurzel.kinder[1].ansichten[0] === idSchnitt && d.a.wurzel.kinder[0].ansichten.length === 3, 'Schnitt allein rechts, die anderen drei links', JSON.stringify(d.a));
  pruef(await haupt.anzahl('.reiterleiste:not([hidden])') === 2 && await haupt.anzahl('.ag-trenner') === 1, 'zwei Reiterleisten und ein Trenner');
  pruef(await haupt.ausw(`return document.querySelectorAll('#ansichtsreiter').length === 1 && document.querySelector('#ansichtsreiter').dataset.gruppe === 'g1' || document.querySelectorAll('#ansichtsreiter').length === 1;`), 'die Id ansichtsreiter gibt es genau einmal');
  const lage = await haupt.ausw(`const f = document.getElementById('ansichtsflaeche').getBoundingClientRect(); const k = [...document.querySelectorAll('.kachel:not(.versteckt)')].map(e => { const r = e.getBoundingClientRect(); return { l: r.left - f.left, t: r.top - f.top, b: r.width, h: r.height, art: e.querySelector('.gr-svg') ? 'plan' : e.querySelector('.d3-wurzel') ? '3d' : '?' }; }); const g = [...document.querySelectorAll('.reiterleiste:not([hidden])')].map(e => { const r = e.getBoundingClientRect(); return { l: r.left - f.left, t: r.top - f.top, b: r.width, h: r.height }; }); return { k, g, f: { b: f.width, h: f.height } };`);
  pruef(lage.k.length === 2 && Math.abs(lage.k[0].h - lage.k[1].h) < 2 && lage.k[0].l < lage.k[1].l && Math.abs(lage.k[0].b + lage.k[1].b + 4 - lage.f.b) < 3, 'zwei Kacheln nebeneinander füllen die Fläche (mit Trenner)', JSON.stringify(lage));
  pruef(lage.g.every((x) => Math.abs(x.h - 22) < 1.5) && Math.abs(lage.g[0].b - lage.k[0].b) < 2 && Math.abs(lage.k[0].t - 22) < 2, 'jede Gruppe hat ihre 22 px hohe Reiterleiste über ihrer Kachel', JSON.stringify(lage));
  await warteBis(() => haupt.ausw(`return document.querySelectorAll('.kachel:not(.versteckt) .gr-svg, .kachel:not(.versteckt) .gr-plan').length >= 2;`), 'beide Pläne gezeichnet', 20000);
  pruef(await haupt.ausw(`const z = await import('/js/zustand.js'); return z.zustand.aktiveAnsicht;`) === SCHNITT, 'der gezogene Schnitt ist die aktive Ansicht');

  // ---------------------------------------------------------------- 3. Trenner ziehen
  abschnitt('3. Trenner mit der Maus ziehen');
  const tr = await haupt.mitteVon('.ag-trenner');
  const breiteVorher = (await haupt.daten()).geo.gruppen[0].b;
  await haupt.ziehen(tr, { x: tr.x - 220, y: tr.y }, { schritte: 8 });
  d = await haupt.daten();
  pruef(Math.abs(d.geo.gruppen[0].b - (breiteVorher - 220)) < 6, `linke Gruppe um 220 px schmaler (${breiteVorher} → ${d.geo.gruppen[0].b})`);
  pruef(d.a.wurzel.anteile[0] < 0.5 && Math.abs(d.a.wurzel.anteile.reduce((s, x) => s + x, 0) - 1) < 1e-9, 'Anteile angepasst, Summe 1', JSON.stringify(d.a.wurzel.anteile));
  pruef(await haupt.ausw(`const f = await import('/js/fenster.js'); const fl = document.getElementById('ansichtsflaeche').getBoundingClientRect(); const geo = f.rechtecke(); const ks = [...document.querySelectorAll('.kachel:not(.versteckt)')].map(e => { const r = e.getBoundingClientRect(); return { l: Math.round(r.left - fl.left), t: Math.round(r.top - fl.top), b: Math.round(r.width), h: Math.round(r.height) }; }); return geo.gruppen.every(g => ks.some(k => Math.abs(k.l - g.inhalt.x) < 2 && Math.abs(k.t - g.inhalt.y) < 2 && Math.abs(k.b - g.inhalt.b) < 2 && Math.abs(k.h - g.inhalt.h) < 2));`), 'die Kacheln folgen den Rechtecken der Gruppen');
  await haupt.ziehen(await haupt.mitteVon('.ag-trenner'), { x: 60, y: tr.y }, { schritte: 8 });
  d = await haupt.daten();
  pruef(d.geo.gruppen[0].b >= 85 && d.geo.gruppen[0].b <= 100, `Mindestgrösse der Gruppe bleibt (${d.geo.gruppen[0].b} px)`);
  await haupt.ziehen(await haupt.mitteVon('.ag-trenner'), { x: tr.x + 20, y: tr.y }, { schritte: 8 });

  // ---------------------------------------------------------------- 4. dritte Gruppe, Bilder
  abschnitt('4. Dritte Gruppe: 3D unter den Schnitt');
  // linke Gruppe wieder breiter, dann 3D unten in die rechte Gruppe
  let t2 = await haupt.mitteVon('.ag-trenner');
  await haupt.ziehen(t2, { x: flaeche.l + flaeche.b * 0.5, y: t2.y }, { schritte: 8 });
  const dreiD = await haupt.reiterMitte('3D Gesamt');
  const g2 = (await haupt.daten()).geo.gruppen[1];
  await haupt.ziehen(dreiD, { x: g2.inhalt.x + flaeche.l + g2.inhalt.b / 2, y: flaeche.t + g2.inhalt.y + g2.inhalt.h * 0.9 });
  d = await haupt.daten();
  pruef(d.geo.gruppen.length === 3 && d.a.wurzel.kinder[1].art === 'teilung' && d.a.wurzel.kinder[1].richtung === 'untereinander', 'drei Gruppen: links, rechts oben, rechts unten', JSON.stringify(d.a));
  pruef(d.a.wurzel.kinder[1].kinder[1].ansichten[0] === id3d, '3D liegt rechts unten');
  await warteBis(() => haupt.ausw(`return !!document.querySelector('.d3-leinwand') && [...document.querySelectorAll('.kachel:not(.versteckt)')].length === 3;`), 'drei Kacheln sichtbar', 20000);
  await warte(1200);
  const hell = async (name, design) => { await haupt.designSetzen(design); await warte(800); await haupt.shot(name + '_' + design); };
  for (const design of ['dunkel', 'hell']) await hell('w2_drei_gruppen', design);
  pruef(await haupt.anzahl('.ag-trenner') === 2, 'zwei Trenner (einer senkrecht, einer waagrecht)');
  pruef(await haupt.ausw(`const a = [...document.querySelectorAll('.ag-trenner')].map(e => e.classList.contains('senkrecht') ? 's' : e.classList.contains('waagrecht') ? 'w' : '?').sort().join(''); return a === 'sw';`), 'Trenner-Richtungen stimmen');

  // ---------------------------------------------------------------- 5. Werkzeug-Ansicht
  abschnitt('5. aktivePlanansicht() folgt der aktiven Gruppe');
  const planArt = () => haupt.ausw(`const f = await import('/js/fenster.js'); const p = f.aktivePlanansicht(); return { art: p?.art ?? null, aktiv: f.aktive()?.id ?? null };`);
  await haupt.klickAn(await haupt.mitteVon('.kachel:not(.versteckt) .gr-svg'));   // irgendein Plan
  let pa = await planArt();
  pruef(pa.art === 'grundriss' || pa.art === 'schnitt', `Klick in einen Plan macht ihn aktiv (${pa.art})`);
  const klickKachel = async (art) => { const mitte = await haupt.ausw(`const k = [...document.querySelectorAll('.kachel:not(.versteckt)')].find(e => ${art === '3d' ? `e.querySelector('.d3-wurzel')` : `e.querySelector('.gr-svg')`}); const r = k.querySelector('.kachel-inhalt').getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + 30 };`); await haupt.klickAn(mitte); };
  await klickKachel('3d');
  pa = await planArt();
  pruef(pa.art === null && pa.aktiv === id3d, '3D aktiv: keine Plan-Ansicht für Werkzeuge', JSON.stringify(pa));
  await haupt.klickAn(await haupt.reiterMitte(NAME1));
  pa = await planArt();
  pruef(pa.art === 'grundriss' && pa.aktiv === idGr1, 'Reiter des Grundrisses in der linken Gruppe: Grundriss ist die Werkzeug-Ansicht', JSON.stringify(pa));
  await haupt.klickAn(await haupt.reiterMitte('Schnitt W2'));
  pa = await planArt();
  pruef(pa.art === 'schnitt' && pa.aktiv === idSchnitt, 'Reiter des Schnitts in der rechten Gruppe: Schnitt ist die Werkzeug-Ansicht', JSON.stringify(pa));
  const nurAktive = await haupt.ausw(`return document.querySelectorAll('.kachel.aktiv').length + '/' + document.querySelectorAll('.reiter.aktiv').length + '/' + document.querySelectorAll('.reiterleiste.aktive-gruppe').length;`);
  pruef(nurAktive === '1/1/1', 'genau eine aktive Kachel, ein aktiver Reiter, eine aktive Gruppe', nurAktive);

  // ---------------------------------------------------------------- 6. Reiter zurück in eine Gruppe
  abschnitt('6. Reiter in eine andere Gruppe ziehen (zusammenlegen)');
  const g1 = (await haupt.daten()).geo.gruppen[0];
  await haupt.ziehen(await haupt.reiterMitte('3D Gesamt'), { x: flaeche.l + g1.leiste.x + 40, y: flaeche.t + g1.leiste.y + 11 });
  d = await haupt.daten();
  pruef(d.geo.gruppen.length === 2 && d.a.wurzel.kinder[0].ansichten.includes(id3d), 'die Gruppe mit 3D ist verschwunden, 3D ist Reiter der linken Gruppe', JSON.stringify(d.a));
  pruef(d.a.wurzel.kinder[0].ansichten[0] === id3d, 'an der Stelle des Loslassens eingefügt (vorn)', JSON.stringify(d.a.wurzel.kinder[0].ansichten));
  pruef(d.a.wurzel.richtung === 'nebeneinander' && d.a.wurzel.kinder.length === 2, 'die Teilung der rechten Seite hat sich aufgelöst');
  // Abbruch mit Esc
  const vorEsc = JSON.stringify((await haupt.daten()).a);
  await haupt.ziehen(await haupt.reiterMitte('Schnitt W2'), { x: flaeche.l + 300, y: flaeche.t + 400 }, { loslassen: false });
  await haupt.send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
  await haupt.send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
  await haupt.send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: flaeche.l + 300, y: flaeche.t + 400, button: 'left', buttons: 0, clickCount: 1 });
  await warte(300);
  pruef(JSON.stringify((await haupt.daten()).a) === vorEsc && (await haupt.anzahl('.ag-ueberlagerung')) === 0, 'Esc bricht das Ziehen ab, nichts ändert sich');
  // kurzes Anklicken ohne Ziehen ändert nichts ausser aktiv
  await haupt.klickAn(await haupt.reiterMitte('Schnitt W2'));
  pruef((await haupt.daten()).geo.gruppen.length === 2, 'Klick auf einen Reiter zieht nichts');

  // ---------------------------------------------------------------- 7. Neuladen
  abschnitt('7. Neuladen stellt die Anordnung wieder her');
  await haupt.klickAn(await haupt.reiterMitte(NAME1));
  await haupt.ziehen(await haupt.mitteVon('.ag-trenner'), { x: flaeche.l + 900, y: flaeche.t + 300 }, { schritte: 6 });
  const vorher = await haupt.daten();
  const gespeichert = await haupt.ausw(`return localStorage.getItem('hlks.anordnung.' + String((await import('/js/zustand.js')).zustand.status.ort).toLowerCase());`);
  pruef(!!gespeichert && JSON.parse(gespeichert).wurzel?.art === 'teilung', 'die Anordnung steht je Projekt in localStorage', gespeichert);
  pruef(!!gespeichert && JSON.stringify(JSON.parse(gespeichert)).includes(GR1) && !JSON.stringify(JSON.parse(gespeichert)).includes(idGr1), 'gemerkt werden Kennzeichen, nicht Ids');
  await haupt.send('Page.reload');
  await warteBis(() => haupt.ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && document.querySelectorAll('.reiterleiste > .reiter').length >= 4 && document.querySelectorAll('.kachel:not(.versteckt)').length >= 2;`), 'nach Neuladen wieder offen', 40000);
  await warte(1200);
  const nachher = await haupt.daten();
  const ohneGeo = (x) => JSON.stringify({ w: x.a.wurzel, ag: x.a.aktiveGruppe });
  pruef(ohneGeo(vorher) === ohneGeo(nachher), 'Gruppen, Reiter, Anteile und aktive Gruppe wie vorher', ohneGeo(vorher) + ' ≠ ' + ohneGeo(nachher));
  pruef(Math.abs(vorher.geo.gruppen[0].b - nachher.geo.gruppen[0].b) < 3, 'Breite der Gruppen wie vorher');
  pruef(await haupt.ausw(`const z = await import('/js/zustand.js'); return z.zustand.aktiveAnsicht;`) === GR1, 'die aktive Ansicht ist wieder der Grundriss');
  // kaputter Eintrag → Vorgabe, ohne Absturz
  await haupt.ausw(`localStorage.setItem('hlks.anordnung.' + String((await import('/js/zustand.js')).zustand.status.ort).toLowerCase(), '{"wurzel":{"art":"teilung","richtung":"x","kinder":[1,2]}}'); return 1;`);
  await haupt.send('Page.reload');
  await warteBis(() => haupt.ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && document.querySelectorAll('.kachel').length >= 1;`), 'nach Neuladen mit kaputtem Eintrag', 40000);
  pruef((await haupt.daten()).geo.gruppen.length === 1, 'kaputter gemerkter Eintrag: normaler Start mit einem Grundriss');
  // Fassung von früher: nur Liste offener Ansichten
  await warte(700);
  await haupt.ausw(`const k = 'hlks.anordnung.' + String((await import('/js/zustand.js')).zustand.status.ort).toLowerCase(); localStorage.removeItem(k); localStorage.setItem('hlks.offeneAnsichten', JSON.stringify([${JSON.stringify(GR1)}, ${JSON.stringify(D3)}, 'GIBTS-NICHT'])); return 1;`);
  await haupt.send('Page.reload');
  await warteBis(() => haupt.ausw(`return document.querySelectorAll('.reiterleiste > .reiter').length === 2;`), 'alte Liste übernommen', 40000);
  pruef(true, 'frühere Liste «offeneAnsichten» wird als eine Gruppe übernommen, Unbekanntes entfällt');

  // ---------------------------------------------------------------- 8. Kacheln, Als Reiter
  abschnitt('8. Kacheln und «Als Reiter»');
  for (const kz of [GR2, SCHNITT]) await haupt.ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(kz)}); return 1;`);
  await haupt.ausw(`const f = await import('/js/fenster.js'); f.nebeneinander(); return 1;`);
  await warte(500);
  d = await haupt.daten();
  pruef(d.geo.gruppen.length === 4 && d.modus === 'nebeneinander' && d.a.wurzel.richtung === 'untereinander' && d.a.wurzel.kinder.length === 2, 'Kacheln: vier Ansichten in 2 × 2', JSON.stringify(d.a.wurzel).slice(0, 200));
  const gr = d.geo.gruppen;
  pruef(Math.max(...gr.map((q) => q.b)) - Math.min(...gr.map((q) => q.b)) <= 2 && Math.max(...gr.map((q) => q.h)) - Math.min(...gr.map((q) => q.h)) <= 2, 'alle vier Kacheln gleich gross', JSON.stringify(gr.map((q) => [q.b, q.h])));
  pruef(await haupt.ausw(`const m = await import('/js/oberflaeche/aktionen.js'); return true;`), 'Aktionsregister lädt');
  await haupt.ausw(`const f = await import('/js/fenster.js'); f.alsReiter(); return 1;`);
  await warte(400);
  d = await haupt.daten();
  pruef(d.a.wurzel.art === 'gruppe' && d.a.wurzel.ansichten.length === 4 && d.modus === 'reiter' && await haupt.anzahl('.ag-trenner') === 0, '«Als Reiter»: wieder eine Gruppe mit vier Reitern');
  pruef(await haupt.ausw(`const m = await import('/js/oberflaeche/aktionen.js'); const a = m.aktionen.get('ansicht.nebeneinander'); const b = m.aktionen.get('ansicht.reiter'); return a.gedrueckt() === false && b.gedrueckt() === true;`), 'Menüband: «Fenster als Reiter» gedrückt, «nebeneinander» nicht');

  // ---------------------------------------------------------------- 9. «▾ n weitere» je Gruppe
  abschnitt('9. «▾ n weitere» je Gruppe');
  // mehr Ansichten öffnen: Grundrisse der übrigen Geschosse und das Schema
  const restliche = grundrisse.slice(2).map((g) => g.kz).concat(['AN-SCHEMA-SAN']);
  for (const kz of restliche) await haupt.ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(kz)}); return 1;`);
  d = await haupt.daten();
  const n = d.a.wurzel.ansichten.length;
  pruef(n >= 6, `genug Ansichten für den Überlauf (${n})`);
  // Gruppe schmal: Schnitt und 3D nach rechts, dann den Trenner weit nach rechts
  await haupt.ziehen(await haupt.reiterMitte('Schnitt W2'), { x: flaeche.l + flaeche.b * 0.9, y: flaeche.t + flaeche.h * 0.5 });
  await haupt.ziehen(await haupt.mitteVon('.ag-trenner'), { x: flaeche.l + flaeche.b * 0.78, y: flaeche.t + 300 }, { schritte: 8 });
  await warte(500);
  const wl = () => haupt.ausw(`return [...document.querySelectorAll('.reiterleiste:not([hidden])')].map(l => { const w = l.querySelector('.reiter-weitere'); const sicht = [...l.querySelectorAll(':scope > .reiter')].filter(r => getComputedStyle(r).display !== 'none'); const aktivSicht = sicht.some(r => r.classList.contains('gruppen-aktiv')); return { weitere: !!w && !w.hidden && getComputedStyle(w).display !== 'none', text: w?.textContent ?? '', sichtbar: sicht.length, gesamt: l.querySelectorAll(':scope > .reiter').length, aktivSicht, breite: l.clientWidth, ueberlauf: l.scrollWidth > l.clientWidth + 1 }; });`);
  let leisten = await wl();
  pruef(leisten.length === 2, 'zwei Gruppen mit Leiste');
  pruef(leisten[0].gesamt > 3, `linke Gruppe hat ${leisten[0].gesamt} Reiter`);
  // Gruppe so schmal, dass der Überlauf kommt: Trenner nach links
  await haupt.ziehen(await haupt.mitteVon('.ag-trenner'), { x: flaeche.l + 260, y: flaeche.t + 300 }, { schritte: 8 });
  await warte(600);
  leisten = await wl();
  pruef(leisten[0].weitere && /^▾ \d+ weitere$/.test(leisten[0].text), `linke (schmale) Gruppe: «${leisten[0].text}» sichtbar`, JSON.stringify(leisten));
  pruef(!leisten[1].weitere, 'rechte Gruppe (wenige Reiter): kein «weitere»', JSON.stringify(leisten[1]));
  pruef(leisten[0].aktivSicht && !leisten[0].ueberlauf, 'der gewählte Reiter bleibt sichtbar, kein Überlauf', JSON.stringify(leisten[0]));
  await haupt.klickAn(await haupt.mitteVon('.reiterleiste:not([hidden]) .reiter-weitere'));
  const liste = await haupt.ausw(`const l = document.querySelector('.reiter-liste:not([hidden])'); return l ? { n: l.querySelectorAll('.rl-eintrag').length, inGruppe: !!l.closest('.reiterleiste'), ersterLeiste: l.closest('.reiterleiste') === document.querySelectorAll('.reiterleiste')[0] } : null;`);
  pruef(!!liste && liste.n === leisten[0].gesamt, 'die Liste zeigt die Ansichten dieser Gruppe', JSON.stringify(liste));
  await haupt.send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
  await haupt.send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
  await warte(200);
  pruef(await haupt.ausw(`return [...document.querySelectorAll('.reiter-liste')].every(l => l.hidden);`), 'Esc schliesst die Liste');
  await haupt.ausw(`const f = await import('/js/fenster.js'); f.alsReiter(); return 1;`);
  await warte(300);

  // ---------------------------------------------------------------- 10. Eigenes Fenster
  abschnitt('10. 3D in eigenem Fenster (Kontextmenü am Reiter)');
  await haupt.ausw(`const f = await import('/js/fenster.js'); for (const o of f.alle()) { const k = (await import('/js/zustand.js')).element(o.id).kennzeichen; if (![${JSON.stringify(GR1)}, ${JSON.stringify(D3)}, ${JSON.stringify(SCHNITT)}].includes(k)) f.schliessen(o.id); } return 1;`);
  await haupt.ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(GR1)}); return 1;`);
  await warte(300);
  const vorZiele = (await haupt.send('Target.getTargets')).targetInfos.filter((t) => t.type === 'page').map((t) => t.targetId);
  const dreiR = await haupt.reiterMitte('3D Gesamt');
  await haupt.klickAn(dreiR, { knopf: 'right', warten: 400 });
  const menue = await haupt.ausw(`return [...document.querySelectorAll('.kmenu button')].map(b => ({ t: b.textContent, g: b.classList.contains('gesperrt') }));`);
  pruef(menue.length === 4 && menue[0].t === 'In eigenem Fenster öffnen' && menue.some((m) => m.t === 'Schliessen'), 'Kontextmenü am Reiter: «In eigenem Fenster öffnen» …', JSON.stringify(menue));
  await haupt.shot('w2_kontextmenue_dunkel');
  const eintrag = await haupt.ausw(`const b = [...document.querySelectorAll('.kmenu button')][0]; const r = b.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2 };`);
  await haupt.klickAn(eintrag, { warten: 600 });
  let zweitZiel = null;
  await warteBis(async () => {
    const l = await (await fetch(`http://127.0.0.1:${CDPPORT}/json/list`)).json();
    zweitZiel = l.find((t) => t.type === 'page' && /fenster=2/.test(t.url));
    return !!zweitZiel;
  }, 'zweites Fenster geöffnet', 15000);
  pruef(/ansicht=AN-3D&fenster=2/.test(zweitZiel.url), 'Adresse des zweiten Fensters: ?ansicht=AN-3D&fenster=2', zweitZiel.url);
  const zweit = await new Seite(zweitZiel, 'zweit').verbinden();
  await zweit.groesse(1000, 700);
  await warteBis(() => zweit.ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && !!document.querySelector('.kachel.aktiv .d3-leinwand');`), '3D im zweiten Fenster', 40000);
  await warte(1500);
  const z1 = await zweit.ausw(`return { fenster: document.body.dataset.fenster, kacheln: document.querySelectorAll('.kachel').length, reiter: document.querySelectorAll('.reiterleiste > .reiter').length, menue: getComputedStyle(document.getElementById('menueband')).visibility, seite: getComputedStyle(document.getElementById('seitenleiste')).visibility, knopf: !!document.querySelector('.ag-zurueck'), titel: document.title, aktiv: (await import('/js/zustand.js')).zustand.aktiveAnsicht };`);
  pruef(z1.fenster === '2' && z1.kacheln === 1 && z1.reiter === 1 && z1.menue === 'hidden' && z1.seite === 'hidden', 'zweites Fenster im Modus «nur Ansicht»: eine Ansicht, ohne Menüband und Seitenleiste', JSON.stringify(z1));
  pruef(z1.knopf && z1.aktiv === D3 && /3D/.test(z1.titel), 'Knopf «Zurück ins Hauptfenster», 3D aktiv, Fenstertitel', JSON.stringify(z1));
  await warte(500);
  d = await haupt.daten();
  pruef(!d.a.wurzel.ansichten.includes(id3d) && JSON.stringify(d.a.ausgelagert) === JSON.stringify([id3d]), 'im Hauptfenster ist 3D aus der Anordnung genommen und als ausgelagert vermerkt', JSON.stringify(d.a));
  pruef(await haupt.ausw(`return ![...document.querySelectorAll('.reiterleiste > .reiter .titel')].some(t => t.textContent.includes('3D'));`), 'im Hauptfenster gibt es keinen 3D-Reiter mehr');
  const fehlerZweit = zweit.fehler.concat(zweit.konsole).filter((x) => !/favicon|404/.test(x));
  pruef(fehlerZweit.length === 0, 'keine Fehler in der Konsole des zweiten Fensters', fehlerZweit.join(' | '));

  abschnitt('11. Live-Abgleich zwischen beiden Fenstern');
  // Auswahl: Hauptfenster → zweites Fenster
  const ein = await haupt.ausw(`const z = await import('/js/zustand.js'); const e = z.alleElemente().find(x => x.klasse === 'Wand') ?? z.alleElemente().find(x => x.schicht !== 'ansicht'); z.auswaehlen(e.id); return { id: e.id, kz: e.kennzeichen, klasse: e.klasse };`);
  await warteBis(() => zweit.ausw(`return (await import('/js/zustand.js')).zustand.auswahl === ${JSON.stringify(ein.id)};`), 'Auswahl im zweiten Fenster', 5000).catch(() => {});
  pruef(await zweit.ausw(`return (await import('/js/zustand.js')).zustand.auswahl;`) === ein.id, `Auswahl im Hauptfenster (${ein.kz}) erscheint im zweiten Fenster`);
  // zweites → Hauptfenster
  const anderes = await zweit.ausw(`const z = await import('/js/zustand.js'); const e = z.alleElemente().filter(x => x.klasse === ${JSON.stringify(ein.klasse)})[1] ?? z.alleElemente().filter(x => x.schicht !== 'ansicht')[3]; z.auswaehlen(e.id); return e.id;`);
  await warteBis(() => haupt.ausw(`return (await import('/js/zustand.js')).zustand.auswahl === ${JSON.stringify(anderes)};`), 'Auswahl im Hauptfenster', 5000).catch(() => {});
  pruef(await haupt.ausw(`return (await import('/js/zustand.js')).zustand.auswahl;`) === anderes, 'Auswahl im zweiten Fenster erscheint im Hauptfenster (Eigenschaftenpalette folgt)');
  pruef(await haupt.ausw(`const e = document.getElementById('eigenschaften'); return e.textContent.length > 20;`), 'die Eigenschaftenpalette des Hauptfensters zeigt die Auswahl');
  await zweit.ausw(`(await import('/js/zustand.js')).auswaehlen(null); return 1;`);
  await warteBis(() => haupt.ausw(`return (await import('/js/zustand.js')).zustand.auswahl === null;`), 'Auswahl aufgehoben', 5000).catch(() => {});
  pruef(await haupt.ausw(`return (await import('/js/zustand.js')).zustand.auswahl;`) === null, 'Auswahl aufheben läuft ebenfalls hinüber');
  // Änderung: Modellstand
  // Verlauf der Ereignisse prüfen: der Zähler der Pakete steigt in beiden Fenstern gleich
  const stand = (s) => s.ausw(`return (await import('/js/zustand.js')).zustand.stand?.letztesPaket ?? null;`);
  const sH = await stand(haupt), sZ = await stand(zweit);
  pruef(sH === sZ && sH !== null, 'beide Fenster haben denselben Modellstand (letztes Paket)', `${sH} / ${sZ}`);
  // gezielte Änderung einer Ansichtseigenschaft im Hauptfenster: Massstab des Schnitts → Zweitfenster (Modell) und umgekehrt
  const massstab = (s, kz) => s.ausw(`return (await import('/js/zustand.js')).element(${JSON.stringify(kz)}).werte.massstab;`);
  const mAlt = await massstab(zweit, SCHNITT);
  const mNeu = mAlt === 50 ? 100 : 50;
  const rm = await haupt.ausw(`const a = await import('/js/api.js'); return await a.befehl('element_aendern', { element: ${JSON.stringify(SCHNITT)}, eigenschaften: { massstab: ${mNeu} } });`);
  pruef(rm.angenommen === true, 'Änderung im Hauptfenster angenommen', JSON.stringify(rm));
  await warteBis(async () => (await massstab(zweit, SCHNITT)) === mNeu, 'Änderung im zweiten Fenster', 8000).catch(() => {});
  pruef(await massstab(zweit, SCHNITT) === mNeu, `Änderung im Hauptfenster (Massstab ${mAlt} → ${mNeu}) erscheint im zweiten Fenster`);
  const rz = await zweit.ausw(`const a = await import('/js/api.js'); return await a.befehl('element_aendern', { element: ${JSON.stringify(SCHNITT)}, eigenschaften: { massstab: ${mAlt} } });`);
  await warteBis(async () => (await massstab(haupt, SCHNITT)) === mAlt, 'Änderung im Hauptfenster', 8000).catch(() => {});
  pruef(rz.angenommen === true && await massstab(haupt, SCHNITT) === mAlt, `Änderung im zweiten Fenster (Massstab zurück auf ${mAlt}) erscheint im Hauptfenster`);
  // Rückgängig im zweiten Fenster wirkt in beiden
  const ru = await zweit.ausw(`const a = await import('/js/api.js'); return await a.befehl('rueckgaengig', {}).catch(e => ({ fehler: String(e.message) }));`);
  console.log('   (Rückgängig) ', JSON.stringify(ru).slice(0, 140));
  await warte(800);
  const mNachUndo = await massstab(haupt, SCHNITT), mNachUndoZ = await massstab(zweit, SCHNITT);
  pruef(mNachUndo === mNachUndoZ && mNachUndo === mNeu, 'nach «Rückgängig» (im zweiten Fenster) steht in beiden Fenstern wieder der vorletzte Wert', `${mNachUndo} / ${mNachUndoZ} / erwartet ${mNeu}`);

  // Bilder: Hauptfenster und zweites Fenster mit 3D
  abschnitt('12. Bilder: Hauptfenster und zweites Fenster (hell und dunkel)');
  await haupt.ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(GR1)}); return 1;`);
  for (const design of ['dunkel', 'hell']) {
    await haupt.designSetzen(design); await zweit.designSetzen(design); await warte(900);
    await haupt.shot(`w2_hauptfenster_3d_${design}`); await zweit.shot(`w2_zweitfenster_3d_${design}`);
  }
  // Grundriss und Schnitt nebeneinander mit Trenner (Bild)
  await haupt.ausw(`const f = await import('/js/fenster.js'); f.alsReiter(); await f.ansichtOeffnen(${JSON.stringify(SCHNITT)}); return 1;`);
  await warte(300);
  const flaeche2 = await haupt.mitteVon('#ansichtsflaeche');
  await haupt.ziehen(await haupt.reiterMitte('Schnitt W2'), { x: flaeche2.l + flaeche2.b * 0.9, y: flaeche2.t + flaeche2.h * 0.5 });
  await warteBis(() => haupt.ausw(`return document.querySelectorAll('.kachel:not(.versteckt) .gr-plan').length >= 2 || document.querySelectorAll('.kachel:not(.versteckt) .gr-svg').length >= 2;`), 'Grundriss und Schnitt gezeichnet', 20000).catch(() => {});
  await warte(1500);
  for (const design of ['dunkel', 'hell']) { await haupt.designSetzen(design); await warte(700); await haupt.shot(`w2_grundriss_schnitt_${design}`); }
  // Andockziele auch hell
  {
    const rt = await haupt.reiterMitte('Schnitt W2');
    const geo = (await haupt.daten()).geo.gruppen[0];
    await haupt.ziehen(rt, { x: flaeche2.l + geo.inhalt.x + geo.inhalt.b * 0.5, y: flaeche2.t + geo.inhalt.y + geo.inhalt.h * 0.5 }, { loslassen: false, mitte: (s) => s.shot('w2_andocken_hell') });
    await haupt.send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
    await haupt.send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Escape', code: 'Escape', windowsVirtualKeyCode: 27 });
    await haupt.send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: 10, y: 10, button: 'left', buttons: 0, clickCount: 1 });
    await warte(300);
  }

  // ---------------------------------------------------------------- 13. Rückgabe
  abschnitt('13. Schliessen des Fensters gibt die Ansicht zurück');
  await zweit.send('Page.close').catch(() => {});
  await warteBis(() => haupt.ausw(`return [...document.querySelectorAll('.reiterleiste > .reiter .titel')].some(t => t.textContent.includes('3D'));`), '3D zurück im Hauptfenster', 12000).catch(() => {});
  d = await haupt.daten();
  pruef(d.a.wurzel && JSON.stringify(d.a).includes(id3d) && d.a.ausgelagert.length === 0, 'nach dem Schliessen des zweiten Fensters ist 3D wieder ein Reiter im Hauptfenster', JSON.stringify(d.a));

  abschnitt('14. Zurück-Knopf');
  await haupt.ausw(`const f = await import('/js/fenster.js'); f.eigenesFensterOeffnen(${JSON.stringify(D3)}, { x: 300, y: 200 }); return 1;`);
  zweitZiel = null;
  await warteBis(async () => { const l = await (await fetch(`http://127.0.0.1:${CDPPORT}/json/list`)).json(); zweitZiel = l.find((t) => t.type === 'page' && /fenster=2/.test(t.url)); return !!zweitZiel; }, 'zweites Fenster (Knopf)', 15000);
  const zweit2 = await new Seite(zweitZiel, 'zweit2').verbinden();
  await warteBis(() => zweit2.ausw(`return !!document.querySelector('.kachel.aktiv .d3-leinwand') && !!document.querySelector('.ag-zurueck');`), '3D im zweiten Fenster', 40000);
  await warte(600);
  pruef(!JSON.stringify((await haupt.daten()).a.wurzel).includes(id3d), '3D im Hauptfenster weg, solange das zweite Fenster offen ist');
  // Hauptfenster neu laden, das zweite Fenster lebt: 3D bleibt dort
  await haupt.send('Page.reload');
  await warteBis(() => haupt.ausw(`const z = await import('/js/zustand.js'); return z.zustand.elemente.size > 50 && document.querySelectorAll('.reiterleiste > .reiter').length >= 1;`), 'Hauptfenster neu geladen', 40000);
  await warte(1500);
  d = await haupt.daten();
  pruef(!JSON.stringify(d.a.wurzel).includes(id3d) && JSON.stringify(d.a.ausgelagert) === JSON.stringify([id3d]), 'Neuladen des Hauptfensters bei lebendem Zweitfenster: 3D bleibt dort, wird nicht doppelt geöffnet', JSON.stringify(d.a));
  await zweit2.ausw(`document.querySelector('.ag-zurueck').click(); return 1;`).catch(() => {});
  await warteBis(() => haupt.ausw(`return [...document.querySelectorAll('.reiterleiste > .reiter .titel')].some(t => t.textContent.includes('3D'));`), '3D zurück durch Knopf', 8000).catch(() => {});
  d = await haupt.daten();
  pruef(JSON.stringify(d.a.wurzel).includes(id3d) && d.a.ausgelagert.length === 0, '«Zurück ins Hauptfenster» bringt 3D zurück und schliesst das Fenster', JSON.stringify(d.a));
  await warte(800);
  const offeneSeiten = (await (await fetch(`http://127.0.0.1:${CDPPORT}/json/list`)).json()).filter((t) => t.type === 'page' && /fenster=2/.test(t.url));
  pruef(offeneSeiten.length === 0, 'das zweite Fenster ist zu');

  abschnitt('15. Zweitfenster neu laden: keine doppelte Ansicht im Hauptfenster');
  await haupt.ausw(`const f = await import('/js/fenster.js'); f.eigenesFensterOeffnen(${JSON.stringify(D3)}, {}); return 1;`);
  zweitZiel = null;
  await warteBis(async () => { const l = await (await fetch(`http://127.0.0.1:${CDPPORT}/json/list`)).json(); zweitZiel = l.find((t) => t.type === 'page' && /fenster=2/.test(t.url)); return !!zweitZiel; }, 'zweites Fenster (3)', 15000);
  const zweit3 = await new Seite(zweitZiel, 'zweit3').verbinden();
  await warteBis(() => zweit3.ausw(`return !!document.querySelector('.kachel.aktiv .d3-leinwand');`), '3D im zweiten Fenster', 40000);
  await zweit3.send('Page.reload');
  await warteBis(() => zweit3.ausw(`return !!document.querySelector('.kachel.aktiv .d3-leinwand');`), '3D nach Neuladen', 40000);
  await warte(2500);
  d = await haupt.daten();
  pruef(!JSON.stringify(d.a.wurzel).includes(id3d) && d.a.ausgelagert.length === 1, 'nach Neuladen des Zweitfensters bleibt 3D dort (Hauptfenster hat sie nicht zurückgeholt)', JSON.stringify(d.a));
  // Ziehen aus dem Browserfenster heraus öffnet ebenfalls ein Fenster
  await zweit3.send('Page.close').catch(() => {});
  await warteBis(() => haupt.ausw(`return [...document.querySelectorAll('.reiterleiste > .reiter .titel')].some(t => t.textContent.includes('3D'));`), '3D wieder da', 12000);

  abschnitt('16. Reiter aus dem Fenster herausziehen');
  const raus = await haupt.reiterMitte('3D Gesamt');
  const vorherSeiten = (await (await fetch(`http://127.0.0.1:${CDPPORT}/json/list`)).json()).filter((t) => t.type === 'page').length;
  await haupt.ziehen(raus, { x: 1700, y: 300 }, { schritte: 10, mitte: async (s) => { pruef(/eigenes Fenster/.test(await s.ausw(`return document.querySelector('.ag-schatten')?.textContent ?? '';`)), 'beim Ziehen aus dem Fenster steht «Loslassen: eigenes Fenster»'); } });
  await warte(1500);
  const nachSeiten = (await (await fetch(`http://127.0.0.1:${CDPPORT}/json/list`)).json()).filter((t) => t.type === 'page');
  const neu = nachSeiten.find((t) => /fenster=2/.test(t.url));
  pruef(nachSeiten.length === vorherSeiten + 1 && !!neu, 'Reiter aus dem Fenster gezogen: zweites Fenster geöffnet', `${vorherSeiten} → ${nachSeiten.length}`);
  if (neu) { const s = await new Seite(neu, 'zweit4').verbinden(); await s.send('Page.close').catch(() => {}); }

  abschnitt('17. Werkzeug im zweiten Fenster: aktivePlanansicht, Wand zeichnen, Live im Hauptfenster');
  await warteBis(async () => !(await (await fetch(`http://127.0.0.1:${CDPPORT}/json/list`)).json()).some((t) => t.type === 'page' && /fenster=/.test(t.url)), 'vorheriges zweites Fenster ist zu', 15000);
  await warteBis(() => haupt.ausw(`return [...document.querySelectorAll('.reiterleiste > .reiter .titel')].some(t => t.textContent.includes('3D'));`), '3D wieder im Hauptfenster', 12000);
  await haupt.ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(GR1)}); f.eigenesFensterOeffnen(${JSON.stringify(GR1)}, { x: 200, y: 100 }); return 1;`);
  zweitZiel = null;
  await warteBis(async () => { const l = await (await fetch(`http://127.0.0.1:${CDPPORT}/json/list`)).json(); zweitZiel = l.find((t) => t.type === 'page' && /fenster=2/.test(t.url)); return !!zweitZiel; }, 'zweites Fenster (Grundriss)', 15000);
  const zweit5 = await new Seite(zweitZiel, 'zweit5').verbinden();
  await zweit5.groesse(1100, 750);
  await warteBis(() => zweit5.ausw(`return !!document.querySelector('.kachel.aktiv .gr-svg') && document.querySelectorAll('.kachel.aktiv .gr-plan .el').length > 5;`), 'Grundriss im zweiten Fenster', 40000);
  await warte(800);
  const pa5 = await zweit5.ausw(`const f = await import('/js/fenster.js'); const b = await import('/js/werkzeuge/basis.js'); const p = f.aktivePlanansicht(); return { art: p?.art ?? null, gleich: !!p && p === b.planAnsicht(), ansicht: (await import('/js/zustand.js')).zustand.aktiveAnsicht };`);
  pruef(pa5.art === 'grundriss' && pa5.gleich && pa5.ansicht === GR1, 'zweites Fenster: aktivePlanansicht() und die Werkzeug-Ansicht sind der Grundriss dieses Fensters', JSON.stringify(pa5));
  const paH = await haupt.ausw(`const f = await import('/js/fenster.js'); const p = f.aktivePlanansicht(); return { art: p?.art ?? null, ansicht: (await import('/js/zustand.js')).zustand.aktiveAnsicht };`);
  pruef(paH.ansicht !== GR1, 'Hauptfenster: aktive Ansicht ist nicht die ausgelagerte', JSON.stringify(paH));
  const waende = (s) => s.ausw(`return (await import('/js/zustand.js')).derKlasse('Wand').length;`);
  const w0 = await waende(haupt);
  await zweit5.ausw(`const r = await import('/js/oberflaeche/register.js'); r.aktionen.get('arch.wand').ausfuehren(); await new Promise(x => setTimeout(x, 300)); const f = document.querySelector('#optionsleiste [data-feld="dicke"]'); f.focus(); f.value = '200'; f.dispatchEvent(new Event('input', { bubbles: true })); f.dispatchEvent(new Event('change', { bubbles: true })); f.blur(); return 1;`);
  await warte(500);
  pruef(await zweit5.ausw(`return (await import('/js/werkzeuge/basis.js')).werkzeugAktiv();`) === 'wand', 'Wand-Werkzeug läuft im zweiten Fenster');
  const pkt = await zweit5.ausw(`const r = document.querySelector('.kachel.aktiv .kachel-inhalt').getBoundingClientRect(); return [{ x: r.left + r.width * 0.2, y: r.top + r.height * 0.15 }, { x: r.left + r.width * 0.4, y: r.top + r.height * 0.15 }];`);
  await zweit5.klickAn(pkt[0]); await zweit5.klickAn(pkt[1]);
  await zweit5.send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Enter', code: 'Enter', windowsVirtualKeyCode: 13, text: String.fromCharCode(13) });   // Enter beendet die Wandkette und sendet sie
  await zweit5.send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Enter', code: 'Enter', windowsVirtualKeyCode: 13 });
  await warteBis(async () => (await waende(haupt)) === w0 + 1, 'neue Wand im Hauptfenster', 10000).catch(() => {});
  const w1 = await waende(haupt);
  if (w1 !== w0 + 1) { console.log('   (Wand) Meldungen:', await zweit5.ausw(`return document.querySelector('#meldungen')?.textContent ?? '';`), '| Optionsleiste:', (await zweit5.ausw(`return document.getElementById('optionsleiste').innerText;`)).replace(/\s+/g, ' ').slice(0, 200)); await zweit5.shot('w2_probe_wand'); }
  pruef(w1 === w0 + 1 && (await waende(zweit5)) === w0 + 1, `im zweiten Fenster gezeichnete Wand erscheint live im Hauptfenster (${w0} → ${w1})`);
  await zweit5.send('Page.close').catch(() => {});
  await warteBis(() => haupt.ausw(`return [...document.querySelectorAll('.reiterleiste > .reiter .titel')].some(t => t.textContent.includes('Grundriss 1.OG'));`), 'Grundriss zurück', 12000).catch(() => {});

  const fehlerHaupt = haupt.fehler.concat(haupt.konsole).filter((x) => !/favicon|404|net::ERR/.test(x));
  pruef(fehlerHaupt.length === 0, 'keine Fehler in der Konsole des Hauptfensters', fehlerHaupt.slice(0, 5).join(' | '));
} catch (e) {
  schlecht++;
  console.log('  FEHL  Abbruch:', e.stack ?? e.message);
  try { await haupt.shot('w2_abbruch'); } catch { /* egal */ }
}

console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
beenden(schlecht === 0 ? 0 : 1);
