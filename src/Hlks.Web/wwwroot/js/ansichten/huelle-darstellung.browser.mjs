// Browser-Integrationstest der Darstellung «Hülle» (Modul 1c, Auftrag C1b): Edge headless über CDP, echte Mausereignisse. Nicht Teil von «node --test».
// Voraussetzung: Server mit einem FRISCHEN Beispielprojekt (dotnet Hlks.Mcp.dll --beispiel <Ordner>/e_KICAD, dann
//   dotnet Hlks.Web.dll --port 5144 --projekt <Ordner>/e_KICAD --akteur T --geraet c1b).
// Aufruf:  HLKS_TEST_SERVER=http://localhost:5144 SHOTS=<Ordner> node huelle-darstellung.browser.mjs
//   BKW=1 übernimmt danach das echte Architekten-IFC (%USERPROFILE%\HLKS_CAD_testdaten\bkw_hauptsitz\BKW-HS_1_ARCH_ARC.ifc) in ein neues
//   Projekt (ARBEIT=<Ordner>, Vorgabe %TEMP%\hlks_c1b\bkw) und dreht das 3D mit Hülle (Bildrate grob, headless = Software-Rendering).
// Prüft: Beispielprojekt mit decke_erzeugen für alle Geschosse; Grundriss auf Hülle schalten (Streifen, Legende, Ein/Aus über den Knopf im Register
// Ansicht, Eigenschaft darstellung), Plan-Ebene huelle; 3D auf Hülle schalten (Flächen, Legende, Klick zeigt Raumgrenze im Infofeld), Bildschirmfotos.
import { spawn } from 'node:child_process';
import { mkdtempSync, writeFileSync, mkdirSync, existsSync, readFileSync, statSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';

const SERVER = process.env.HLKS_TEST_SERVER ?? 'http://localhost:5144';
const CDPPORT = Number(process.env.HLKS_CDP_PORT ?? 9344);
const edge = process.env.HLKS_EDGE ?? 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe';
const SHOTS = process.env.SHOTS ?? path.join(tmpdir(), 'hlks_c1b');
mkdirSync(SHOTS, { recursive: true });

const profil = mkdtempSync(path.join(tmpdir(), 'edgec1b-'));
const proc = spawn(edge, ['--headless=new', `--remote-debugging-port=${CDPPORT}`, `--user-data-dir=${profil}`, '--window-size=1600,950', '--no-first-run', '--enable-unsafe-swiftshader', 'about:blank'], { stdio: 'ignore' });
const warte = (ms) => new Promise((r) => setTimeout(r, ms));
let ziel = null;
for (let i = 0; i < 50; i++) {
  try { const r = await fetch(`http://127.0.0.1:${CDPPORT}/json/new?${encodeURIComponent(`${SERVER}/`)}`, { method: 'PUT' }); ziel = await r.json(); break; } catch { await warte(300); }
}
if (!ziel) { console.error('Edge nicht erreichbar'); proc.kill(); process.exit(2); }
const ws = new WebSocket(ziel.webSocketDebuggerUrl);
await new Promise((r) => (ws.onopen = r));
let nr = 0; const offen = new Map(); const fehler = []; const konsole = [];
ws.onmessage = (m) => {
  const d = JSON.parse(m.data);
  if (d.id && offen.has(d.id)) { offen.get(d.id)(d); offen.delete(d.id); return; }
  if (d.method === 'Runtime.exceptionThrown') fehler.push(d.params.exceptionDetails.exception?.description ?? d.params.exceptionDetails.text);
  if (d.method === 'Runtime.consoleAPICalled' && ['error', 'warning'].includes(d.params.type)) konsole.push(`${d.params.type}: ${d.params.args.map((a) => a.value ?? a.description).join(' ')}`);
};
const send = (method, params = {}) => new Promise((res, rej) => { const id = ++nr; offen.set(id, (d) => (d.error ? rej(new Error(JSON.stringify(d.error))) : res(d.result))); ws.send(JSON.stringify({ id, method, params })); });
async function ausw(expr) {
  const r = await send('Runtime.evaluate', { expression: `(async()=>{${expr}})()`, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
  return r.result.value;
}
await send('Runtime.enable'); await send('Page.enable'); await send('DOM.enable');
await send('Emulation.setDeviceMetricsOverride', { width: 1600, height: 950, deviceScaleFactor: 1, mobile: false });

let ok = 0, schlecht = 0;
const pruef = (bed, t, info = '') => { if (bed) { ok++; console.log('  ok   ', t); } else { schlecht++; console.log('  FEHL ', t, info); } };
const abschnitt = (t) => console.log('\n== ' + t);
const shot = async (name) => { const r = await send('Page.captureScreenshot', { format: 'png' }); writeFileSync(path.join(SHOTS, name + '.png'), Buffer.from(r.data, 'base64')); console.log('   Bild:', path.join(SHOTS, name + '.png')); };
const warteBis = async (fn, t, ms = 15000) => { const t0 = Date.now(); while (Date.now() - t0 < ms) { try { if (await fn()) return true; } catch { /* weiter */ } await warte(150); } throw new Error('Zeitüberschreitung: ' + t); };
const zustandDaten = (ausdruck) => ausw(`const z = await import('/js/zustand.js'); return (${ausdruck});`);
const knopf = (id) => ausw(`const k = document.querySelector('.mb-knopf[data-id="${id}"]'); if (!k) return false; k.click(); return true;`);
const register = (name) => ausw(`[...document.querySelectorAll('.mb-tab')].find(t => t.textContent === ${JSON.stringify(name)}).click(); return true;`);
const kern = async (art, name, body, trocken = false) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const maus = async (typ, x, y, extra = {}) => send('Input.dispatchMouseEvent', { type: typ, x, y, button: 'left', buttons: typ === 'mouseReleased' ? 0 : 1, clickCount: 1, ...extra });
const klick = async (x, y) => { await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x, y }); await maus('mousePressed', x, y); await maus('mouseReleased', x, y); };
/** Mit dem Mausrad an den Mittelpunkt der Ansicht heranzoomen (Schritte à 300). */
const heranzoomen = async (selektor, schritte) => {
  const b = await ausw(`const r = document.querySelector(${JSON.stringify(selektor)}).getBoundingClientRect(); return { x: r.left + r.width / 2 + 10, y: r.top + r.height / 2 + 10 };`);
  for (let i = 0; i < schritte; i++) { await send('Input.dispatchMouseEvent', { type: 'mouseWheel', x: b.x, y: b.y, deltaX: 0, deltaY: -300 }); await warte(120); }
};
const aktiveAnsichtOeffnen = (kz) => ausw(`const f = await import('/js/fenster.js'); await f.ansichtOeffnen(${JSON.stringify(kz)}); return true;`);

async function appBereit() {
  await warteBis(async () => (await ausw(`return (await import('/js/zustand.js')).zustand.elemente.size > 50;`)), 'App gestartet', 30000);
  await warte(1000);
}

// Seiten-Hilfen
const huelleKnopfGedrueckt = () => ausw(`return document.querySelector('.mb-knopf[data-id="ansicht.huelle"]')?.classList.contains('gedrueckt') ?? null;`);
const legendeZeilen = (sel) => ausw(`const l = document.querySelector(${JSON.stringify(sel)}); if (!l || l.hidden) return null; return [...l.querySelectorAll('.hl-zeile')].map(z => ({ a: z.dataset.angrenzung, farbe: z.querySelector('.hl-feld').style.background, text: z.innerText.replace(/\\s+/g, ' ') }));`);

// ------------------------------------------------------------------ Start
await appBereit();

abschnitt('Beispielprojekt: Boden, Decken und Dach erzeugen (decke_erzeugen für alle Geschosse)');
const modell = await (await fetch(`${SERVER}/api/modell`)).json();
const geschosse = modell.elemente.filter((e) => e.klasse === 'Geschoss').sort((a, b) => a.werte.kote - b.werte.kote);
console.log('   Geschosse:', geschosse.map((g) => `${g.kennzeichen} (${g.werte.kote})`).join(', '));
const erzeugt = [];
for (const [i, g] of geschosse.entries()) {
  const arten = [i === 0 ? 'boden' : null, 'decke', i === geschosse.length - 1 ? 'dach' : null].filter(Boolean);
  for (const art of arten) {
    const r = await kern('befehl', 'decke_erzeugen', { geschoss: g.kennzeichen, art, dicke: 250 });
    erzeugt.push(`${g.kennzeichen}/${art}: ${r.angenommen ? 'ok' : r.fehler}`);
    pruef(r.angenommen, `decke_erzeugen ${g.kennzeichen} ${art}`, r.fehler);
  }
}
console.log('   ', erzeugt.join(' | '));
await warte(1500);

// ------------------------------------------------------------------ Grundriss
abschnitt('Grundriss EG auf Hülle schalten');
await aktiveAnsichtOeffnen('AN-GR-EG');
await warteBis(async () => (await ausw(`return document.querySelectorAll('.kachel.aktiv .gr-plan .el').length;`)) > 5, 'Grundriss gezeichnet', 20000);
await warte(500);
pruef((await ausw(`return document.querySelectorAll('.kachel.aktiv .gr-plan .e-huelle').length;`)) === 0, 'Standard: keine Ebene huelle im SVG');
pruef((await ausw(`return document.querySelector('.kachel.aktiv .gr-legende').hidden;`)) === true, 'Standard: keine Legende');
await shot('01-grundriss-standard');
await register('Ansicht');
await warte(300);
pruef((await ausw(`return !!document.querySelector('.mb-knopf[data-id="ansicht.huelle"]');`)), 'Knopf «Hülle» im Register Ansicht');
const gruppe = await ausw(`return document.querySelector('.mb-knopf[data-id="ansicht.huelle"]').closest('.mb-gruppe').querySelector('.mb-gruppenname').textContent;`);
pruef(gruppe === 'Darstellung', 'Knopf steht in der Gruppe «Darstellung»', gruppe);
pruef((await huelleKnopfGedrueckt()) === false, 'Knopf nicht gedrückt');
await knopf('ansicht.huelle');
await warteBis(async () => (await ausw(`return document.querySelectorAll('.kachel.aktiv .gr-plan .e-huelle').length;`)) > 0, 'Hülle im Grundriss', 20000);
await warte(800);
pruef((await zustandDaten(`z.element('AN-GR-EG').werte.darstellung`)) === 'huelle', 'Eigenschaft darstellung = huelle (über element_aendern)');
pruef((await huelleKnopfGedrueckt()) === true, 'Knopf gedrückt');
const svgInfo = await ausw(`const g = document.querySelector('.kachel.aktiv .gr-plan .e-huelle'); const farben = {}; for (const p of g.querySelectorAll('path')) { const f = p.getAttribute('fill'); farben[f] = (farben[f] ?? 0) + 1; } const alle = [...document.querySelectorAll('.kachel.aktiv .gr-plan > g')].map(x => x.getAttribute('class')); return { pfade: g.querySelectorAll('path').length, farben, reihenfolge: alle.indexOf('e-ohne e-huelle') , raum: alle.findIndex(c => c.includes('e-raum')), wand: alle.findIndex(c => c.includes('e-wand')), hit: g.closest('.e-ohne') !== null };`);
console.log('   Streifen:', JSON.stringify(svgInfo));
pruef(svgInfo.pfade >= 8, 'Streifen im SVG', JSON.stringify(svgInfo));
pruef(Object.keys(svgInfo.farben).includes('#e0482f'), 'Streifen in der Farbe «aussen»');
pruef(svgInfo.hit && !(await ausw(`return document.querySelector('.kachel.aktiv .gr-plan .e-huelle').hasAttribute('data-id');`)), 'Hüllteil ohne Id (nicht anklickbar)');
const rei = await ausw(`const klassen = [...document.querySelectorAll('.kachel.aktiv .gr-plan > g')].map(x => x.getAttribute('class')); const ersterRaum = klassen.findIndex(c => c.includes('e-raum')), letzterRaum = klassen.map((c, i) => c.includes('e-raum') ? i : -1).filter(i => i >= 0).pop(), huelle = klassen.findIndex(c => c.includes('e-huelle')), ersteWand = klassen.findIndex(c => c.includes('e-wand')); return { letzterRaum, huelle, ersteWand };`);
pruef(rei.letzterRaum < rei.huelle && rei.huelle < rei.ersteWand, 'Ebenenfolge im SVG: Räume < Hülle < Wände', JSON.stringify(rei));
await warteBis(async () => (await legendeZeilen('.kachel.aktiv .gr-legende')) !== null, 'Legende im Grundriss', 20000);
const lg = await legendeZeilen('.kachel.aktiv .gr-legende');
console.log('   Legende Grundriss:', JSON.stringify(lg));
pruef(lg.length === 4 && lg.map((z) => z.a).join() === 'aussen,erdreich,unbeheizt,beheizt', 'Legende mit vier Angrenzungen');
pruef(lg.every((z) => /m²/.test(z.text)) && (lg[0].text.match(/[\d’.]+ m²/) ?? [''])[0] !== '0.0 m²', 'Legende mit Flächensummen');
const sumKern = await kern('abfrage', 'raumgrenzen', { geschoss: 'EG' });
const aussenKern = sumKern.summen.jeAngrenzung.aussen?.flaeche_m2;
pruef(lg[0].text.includes((aussenKern).toFixed(1).replace(/\B(?=(\d{3})+(?!\d))/g, '’')), `Summe «aussen» = Abfrage raumgrenzen (${aussenKern} m²)`, lg[0].text);
await ausw(`const f = await import('/js/fenster.js'); f.zoomAlles(); return 1;`);
await warte(800);
await shot('02-grundriss-huelle-dunkel');
// hell
await ausw(`const d = await import('/js/oberflaeche/design.js'); d.designUmschalten(); return 1;`);
await warte(500);
await shot('03-grundriss-huelle-hell');
await ausw(`const d = await import('/js/oberflaeche/design.js'); d.designUmschalten(); return 1;`);
await warte(300);
// Geschoss UG (Erdreich)
await aktiveAnsichtOeffnen('AN-GR-UG');
await warte(500);
await register('Ansicht');
await warteBis(async () => (await huelleKnopfGedrueckt()) === false, 'UG ohne Hülle', 5000);
await knopf('ansicht.huelle');
await warteBis(async () => (await legendeZeilen('.kachel.aktiv .gr-legende')) !== null, 'Legende UG', 20000);
await warte(1000);
const lgUg = await legendeZeilen('.kachel.aktiv .gr-legende');
console.log('   Legende UG:', JSON.stringify(lgUg));
await shot('04-grundriss-ug-huelle');
// aus
await knopf('ansicht.huelle');
await warteBis(async () => (await ausw(`return document.querySelectorAll('.kachel.aktiv .gr-plan .e-huelle').length;`)) === 0, 'Hülle UG aus', 20000);
pruef((await ausw(`return document.querySelector('.kachel.aktiv .gr-legende').hidden;`)) === true, 'Aus: Legende weg');
pruef((await zustandDaten(`z.element('AN-GR-UG').werte.darstellung ?? null`)) === null, 'Aus: Eigenschaft darstellung leer');
// Rückgängig
await send('Input.dispatchKeyEvent', { type: 'rawKeyDown', key: 'z', code: 'KeyZ', windowsVirtualKeyCode: 90, modifiers: 2 });
await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'z', code: 'KeyZ', windowsVirtualKeyCode: 90, modifiers: 2 });
await warte(1500);
pruef((await zustandDaten(`z.element('AN-GR-UG').werte.darstellung ?? null`)) === 'huelle', 'Strg+Z macht das Ausschalten rückgängig');
await warteBis(async () => (await ausw(`return document.querySelectorAll('.kachel.aktiv .gr-plan .e-huelle').length;`)) > 0, 'Hülle UG wieder da', 20000);

// ------------------------------------------------------------------ 3D
abschnitt('3D auf Hülle schalten');
await aktiveAnsichtOeffnen('AN-3D');
await warteBis(async () => (await ausw(`return !!document.querySelector('.kachel.aktiv .d3-leinwand') && document.querySelector('.kachel.aktiv .d3-hinweis')?.hidden;`)), '3D gezeichnet', 30000);
await warte(1500);
await shot('05-3d-standard');
await register('Ansicht');
await warte(300);
await knopf('ansicht.huelle');
await warteBis(async () => !!(await ausw(`return document.querySelector('.kachel.aktiv .d3-wurzel')?.dataset.huelle ?? null;`)), '3D-Hülle geladen', 30000);
await warte(1500);
const h3 = JSON.parse(await ausw(`return document.querySelector('.kachel.aktiv .d3-wurzel').dataset.huelle;`));
console.log('   3D-Hülle:', JSON.stringify(h3));
pruef(h3.zeilen > 50 && h3.flaechen > 100, 'Raumgrenzen als Flächen geladen', JSON.stringify(h3));
const lg3 = await legendeZeilen('.kachel.aktiv .d3-legende');
console.log('   Legende 3D:', JSON.stringify(lg3));
pruef(lg3?.length === 4, 'Legende im 3D (vier Angrenzungen)');
const sumAlle = await kern('abfrage', 'raumgrenzen', {});
pruef(lg3[0].text.includes(sumAlle.summen.jeAngrenzung.aussen.flaeche_m2.toFixed(1).replace(/\B(?=(\d{3})+(?!\d))/g, '’')), 'Summe «aussen» im 3D = Abfrage', lg3[0].text);
await shot('06-3d-huelle');
// Klick auf eine Fläche
const r = await ausw(`const b = document.querySelector('.kachel.aktiv .d3-leinwand').getBoundingClientRect(); return { x: b.left, y: b.top, w: b.width, h: b.height };`);
let info = null, getroffen = null;
for (let j = 3; j <= 9 && !info; j++) for (let i = 2; i <= 10 && !info; i++) {
  const x = r.x + r.w * i / 12, y = r.y + r.h * j / 12;
  await klick(x, y);
  await warte(120);
  info = await ausw(`const f = document.querySelector('.kachel.aktiv .d3-info'); return f.hidden ? null : f.innerText.replace(/\\s+/g, ' ');`);
  if (info) getroffen = [Math.round(x), Math.round(y)];
}
console.log('   Info nach Klick', JSON.stringify(getroffen), ':', info);
pruef(!!info && /Raum/.test(info) && /Nachbar/.test(info) && /Fläche/.test(info), 'Klick auf eine Hüllfläche zeigt Raum, Nachbar, Bauteil/Fläche im Infofeld', String(info));
await shot('07-3d-huelle-info');
// leer klicken: Info weg
await klick(r.x + 4, r.y + 4);
await warte(300);
pruef((await ausw(`return document.querySelector('.kachel.aktiv .d3-info').hidden;`)) === true, 'Klick ins Leere schliesst das Infofeld');
// Aus
await knopf('ansicht.huelle');
await warteBis(async () => (await ausw(`return document.querySelector('.kachel.aktiv .d3-legende').hidden;`)) === true, '3D Hülle aus', 10000);
pruef(true, '3D Hülle ausgeschaltet: Legende weg');
await knopf('ansicht.huelle');
await warteBis(async () => (await ausw(`return document.querySelector('.kachel.aktiv .d3-legende').hidden;`)) === false, '3D Hülle wieder an', 30000);

// ------------------------------------------------------------------ BKW
if (process.env.BKW) {
  const ifc = path.join(process.env.USERPROFILE ?? '', 'HLKS_CAD_testdaten', 'bkw_hauptsitz', 'BKW-HS_1_ARCH_ARC.ifc');
  if (!existsSync(ifc)) console.log('\nBKW-Datei fehlt:', ifc);
  else {
    abschnitt('BKW Hauptsitz: Architektur-IFC übernehmen, 3D mit Hülle drehen');
    const arbeit = process.env.ARBEIT ?? path.join(tmpdir(), 'hlks_c1b', 'bkw');
    mkdirSync(arbeit, { recursive: true });
    const ordner = path.join(arbeit, 'e_KICAD');
    let t0 = Date.now();
    let a = await (await fetch(`${SERVER}/api/projekt/anlegen`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ ordner, name: 'BKW Test C1b', beispiel: false }) })).json();
    console.log('   Projekt angelegt:', JSON.stringify(a).slice(0, 120));
    const up = await (await fetch(`${SERVER}/api/unterlagen?name=BKW-HS_1_ARCH_ARC.ifc`, { method: 'POST', body: readFileSync(ifc) })).json();
    console.log(`   Hochgeladen (${(statSync(ifc).size / 1e6).toFixed(0)} MB, ${((Date.now() - t0) / 1000).toFixed(1)} s):`, JSON.stringify(up));
    t0 = Date.now();
    const u = await kern('befehl', 'ifc_uebernehmen', { datei: up.name });
    console.log(`   Übernommen in ${((Date.now() - t0) / 1000).toFixed(1)} s:`, u.angenommen, (u.meldung ?? u.fehler ?? '').slice(0, 160));
    pruef(u.angenommen, 'BKW übernommen', u.fehler);
    // Ansichten: Grundriss EG und 3D
    const g = (await (await fetch(`${SERVER}/api/modell`)).json()).elemente.filter((e) => e.klasse === 'Geschoss');
    const eg = g.find((x) => x.kennzeichen === 'EG') ?? g[0];
    await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-3D-BKW', name: '3D BKW', eigenschaften: { art: '3d', detailgrad: 'mittel' } });
    await kern('befehl', 'element_anlegen', { klasse: 'Ansicht', kennzeichen: 'AN-GR-BKW', name: 'Grundriss BKW', geschoss: eg.kennzeichen, eigenschaften: { art: 'grundriss', massstab: 200, detailgrad: 'mittel', schnitthoehe: 1000 } });
    await send('Page.navigate', { url: `${SERVER}/` });
    await warte(2500);
    await warteBis(async () => (await ausw(`return (await import('/js/zustand.js')).zustand.elemente.size > 1000;`)), 'BKW im Browser', 120000);
    // Grundriss EG mit Hülle
    await aktiveAnsichtOeffnen('AN-GR-BKW');
    await warteBis(async () => (await ausw(`return document.querySelectorAll('.kachel.aktiv .gr-plan .el').length;`)) > 20, 'BKW-Grundriss gezeichnet', 120000);
    await warte(1000);
    t0 = Date.now();
    await kern('befehl', 'element_aendern', { element: 'AN-GR-BKW', eigenschaften: { darstellung: 'huelle' } });
    await warteBis(async () => (await ausw(`return document.querySelectorAll('.kachel.aktiv .gr-plan .e-huelle path').length;`)) > 0, 'BKW-Hülle im Grundriss', 180000);
    console.log(`   BKW-Grundriss ${eg.kennzeichen} mit Hülle nach ${((Date.now() - t0) / 1000).toFixed(1)} s, Pfade: ${await ausw(`return document.querySelectorAll('.kachel.aktiv .gr-plan .e-huelle path').length;`)}`);
    await warteBis(async () => (await legendeZeilen('.kachel.aktiv .gr-legende')) !== null, 'BKW-Legende', 60000);
    await ausw(`const f = await import('/js/fenster.js'); f.zoomAlles(); return 1;`);
    await warte(1500);
    await shot('10-bkw-grundriss-huelle');
    await heranzoomen('.kachel.aktiv .gr-svg', 9);
    await warte(800);
    await shot('10b-bkw-grundriss-huelle-nah');
    // 3D
    await aktiveAnsichtOeffnen('AN-3D-BKW');
    await warteBis(async () => (await ausw(`return !!document.querySelector('.kachel.aktiv .d3-leinwand') && document.querySelector('.kachel.aktiv .d3-hinweis')?.hidden;`)), 'BKW-3D gezeichnet', 180000);
    await warte(2000);
    await shot('11-bkw-3d-standard');
    t0 = Date.now();
    await kern('befehl', 'element_aendern', { element: 'AN-3D-BKW', eigenschaften: { darstellung: 'huelle' } });
    await warteBis(async () => !!(await ausw(`return document.querySelector('.kachel.aktiv .d3-wurzel')?.dataset.huelle ?? null;`)), 'BKW-3D-Hülle', 300000);
    const gesamt = (Date.now() - t0) / 1000;
    await warte(2000);
    const hb = JSON.parse(await ausw(`return document.querySelector('.kachel.aktiv .d3-wurzel').dataset.huelle;`));
    console.log(`   BKW-3D-Hülle nach ${gesamt.toFixed(1)} s:`, JSON.stringify(hb));
    pruef(hb.zeilen > 5000, 'BKW: mehr als 5000 Raumgrenzen als Flächen', JSON.stringify(hb));
    await shot('12-bkw-3d-huelle');
    await heranzoomen('.kachel.aktiv .d3-leinwand', 6);
    await warte(1500);
    await shot('12b-bkw-3d-huelle-nah');
    // Drehen: 4 s lang ziehen, rAF-Zähler in der Seite
    const rr = await ausw(`const b = document.querySelector('.kachel.aktiv .d3-leinwand').getBoundingClientRect(); return { x: b.left, y: b.top, w: b.width, h: b.height };`);
    await ausw(`window.__fr = { n: 0, an: true, t0: performance.now() }; (function s() { if (!window.__fr.an) return; window.__fr.n++; requestAnimationFrame(s); })(); return 1;`);
    const cx = rr.x + rr.w / 2, cy = rr.y + rr.h / 2;
    await maus('mousePressed', cx, cy);
    const t1 = Date.now();
    let schritte = 0;
    while (Date.now() - t1 < 4000) {
      const w = (schritte % 120) / 120 * 2 * Math.PI;
      await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: cx + Math.sin(w) * 200, y: cy + Math.cos(w) * 40, button: 'left', buttons: 1 });
      schritte++;
    }
    await maus('mouseReleased', cx, cy);
    const fr = await ausw(`window.__fr.an = false; return { n: window.__fr.n, ms: performance.now() - window.__fr.t0 };`);
    console.log(`   Drehen: ${schritte} Mausschritte in ${((Date.now() - t1) / 1000).toFixed(1)} s, Seiten-Frames ${fr.n} in ${(fr.ms / 1000).toFixed(1)} s = ${(fr.n / fr.ms * 1000).toFixed(1)} Bilder/s (headless, Software-Rendering)`);
    await warte(500);
    await shot('13-bkw-3d-huelle-gedreht');
    // ohne Hülle zum Vergleich
    await kern('befehl', 'element_aendern', { element: 'AN-3D-BKW', eigenschaften: { darstellung: null } });
    await warte(2000);
    await ausw(`window.__fr = { n: 0, an: true, t0: performance.now() }; (function s() { if (!window.__fr.an) return; window.__fr.n++; requestAnimationFrame(s); })(); return 1;`);
    await maus('mousePressed', cx, cy);
    const t2 = Date.now();
    let s2 = 0;
    while (Date.now() - t2 < 4000) {
      const w = (s2 % 120) / 120 * 2 * Math.PI;
      await send('Input.dispatchMouseEvent', { type: 'mouseMoved', x: cx + Math.sin(w) * 200, y: cy + Math.cos(w) * 40, button: 'left', buttons: 1 });
      s2++;
    }
    await maus('mouseReleased', cx, cy);
    const fr2 = await ausw(`window.__fr.an = false; return { n: window.__fr.n, ms: performance.now() - window.__fr.t0 };`);
    console.log(`   Zum Vergleich ohne Hülle: ${s2} Mausschritte, ${(fr2.n / fr2.ms * 1000).toFixed(1)} Bilder/s`);
  }
}

console.log(`\n${ok} bestanden, ${schlecht} fehlgeschlagen`);
if (fehler.length) console.log('JS-Ausnahmen:\n' + [...new Set(fehler)].join('\n'));
if (konsole.length) console.log('Konsole (Fehler/Warnungen):\n' + [...new Set(konsole)].slice(0, 15).join('\n'));
ws.close(); proc.kill();
process.exit(schlecht ? 1 : 0);
