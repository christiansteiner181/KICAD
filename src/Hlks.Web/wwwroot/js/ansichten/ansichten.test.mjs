// Tests der Ansichtsmodule und des Designs hell/dunkel – die Teile ohne Browser:
//   plangrafik.js (Primitive des Kerns → SVG), koerper3d.js (Körper des Kerns → three-Objekte), Listenaufbereitung,
//   Planblatt-Aktionen, Zuordnung der Ansichtsarten in fenster.js, Design (CSS-Variablen, Umschalten).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/ansichten.test.mjs
// Ist ein Testserver genannt (HLKS_TESTSERVER bzw. HLKS_TEST_SERVER=<url>, Server mit dem Beispielprojekt),
// laufen zusätzlich die echten Grafikdaten des Kerns durch beide Renderer.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { register } from 'node:module';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';
import { readFileSync, existsSync } from 'node:fs';

// «three» ist im Browser über die Importmap erreichbar; in Node per Auflöser auf die lokale Datei umlenken
const hier = path.dirname(fileURLToPath(import.meta.url));
const threeUrl = pathToFileURL(path.resolve(hier, '../../lib/three/three.module.js')).href;
register('data:text/javascript,' + encodeURIComponent(
  `export async function resolve(s, c, n) { if (s === 'three') return { url: ${JSON.stringify(threeUrl)}, shortCircuit: true }; return n(s, c); }`));

const fmt = await import('./listenformat.js');
const h = await import('./hilfen.js');
const pg = await import('./plangrafik.js');
const k3 = await import('./koerper3d.js');
const THREE = await import(threeUrl);

const wwwroot = path.resolve(hier, '../..');
const kernOrdner = path.resolve(hier, '../../../../Hlks.Kern');
const lies = (rel) => readFileSync(path.join(wwwroot, rel), 'utf8');
const kernQuelle = (rel) => { const p = path.join(kernOrdner, rel); return existsSync(p) ? readFileSync(p, 'utf8') : null; };

// ---------- Hilfen ----------
test('Maskieren und Farbprüfung', () => {
  assert.equal(h.esc('<a href="x">&'), '&lt;a href=&quot;x&quot;&gt;&amp;');
  assert.equal(h.farbeSicher('#8b5a2b'), '#8b5a2b');
  assert.equal(h.farbeSicher('red" onload="x'), '#666666');
  assert.deepEqual(h.ansichtsWerte({ werte: { massstab: 50, detailgrad: 'fein' } }), { massstab: 50, detailgrad: 'fein' });
  assert.deepEqual(h.ansichtsWerte({ werte: { massstab: 0, detailgrad: 'x' } }), { massstab: 100, detailgrad: 'mittel' });
});

// ---------- Plangrafik → SVG ----------
const ctx = (massstab = 50, schraffuren = new Map()) => ({ massstab, schraffuren, muster: new Map() });
const prim = (art, extra = {}, stil = {}) => ({ art, stil: { stift: 'duenn', linie: 'voll', ...stil }, ...extra });
const BETON = { name: 'beton', art: 'linien', winkel: [45, 135], abstandMm: 1.2, annahme: false };
const PUTZ = { name: 'putz', art: 'punkte', winkel: [], abstandMm: 0.8, annahme: false };
const VOLL = { name: 'voll', art: 'voll', winkel: [], abstandMm: 0, annahme: false };
const schraffuren = pg.schraffurMap([BETON, PUTZ, VOLL, { name: 'glas', art: 'linien', winkel: [0], abstandMm: 0.8 }]);

test('Ebenenreihenfolge: wie Ebenen.Reihenfolge im Kern; unbekannte Ebenen zählen wie «sonst»', () => {
  const cs = kernQuelle('Grafik/Grafiktypen.cs');
  if (cs) {
    const liste = [...cs.match(/Reihenfolge\s*=\s*\[([^\]]*)\]/)[1].matchAll(/"([^"]+)"/g)].map((m) => m[1]);
    assert.deepEqual(pg.EBENEN, liste, 'EBENEN weicht von Ebenen.Reihenfolge ab');
  }
  assert.equal(pg.EBENEN[0], 'grundlage');   // Unterlagen ganz unten
  assert.equal(pg.EBENEN[1], 'raum');
  assert.equal(pg.EBENEN.at(-1), 'symbol');
  assert.equal(pg.ebenenRang('gibtsnicht'), pg.ebenenRang('sonst'));
  assert.ok(pg.ebenenRang('grundlage') < pg.ebenenRang('raum') && pg.ebenenRang('wand') < pg.ebenenRang('leitung') && pg.ebenenRang('apparat') < pg.ebenenRang('beschriftung'));
});

test('Ebenenreihenfolge: Teile werden von unten nach oben gezeichnet, innerhalb einer Ebene bleibt die Listenreihenfolge', () => {
  const teile = [{ id: 'b1', ebene: 'beschriftung' }, { id: 'l1', ebene: 'leitung' }, { id: 'w1', ebene: 'wand' }, { id: 'l2', ebene: 'leitung' }, { id: 'x', ebene: 'unbekannt' }, { id: 'r', ebene: 'raum' }];
  assert.deepEqual(pg.nachEbenen(teile).map((t) => t.id), ['r', 'w1', 'l1', 'l2', 'x', 'b1']);
  // im Markup in derselben Reihenfolge
  const poly = (id, ebene) => ({ id, ebene, primitive: [prim('polygon', { punkte: [[0, 0], [1, 0], [1, 1]] })] });
  const svg = pg.planSvg({ massstab: 50, teile: [poly('top', 'symbol'), poly('unten', 'raum'), poly('mitte', 'leitung')] });
  const pos = ['unten', 'mitte', 'top'].map((i) => svg.indexOf(`data-id="${i}"`));
  assert.ok(pos[0] > 0 && pos[0] < pos[1] && pos[1] < pos[2], String(pos));
});

test('Stiftbreiten: duenn 0.18, mittel 0.35, dick 0.70 mm Papier × Massstab in Modell-mm (wie Stifte.Mm im Kern)', () => {
  assert.equal(pg.strichbreite('duenn', 50), 9);
  assert.equal(pg.strichbreite('mittel', 50), 17.5);
  assert.equal(pg.strichbreite('dick', 50), 35);
  assert.equal(pg.strichbreite('dick', 100), 70);
  assert.equal(pg.strichbreite(undefined, 50), 9);           // wie Kern: unbekannt = dünn
  const cs = kernQuelle('Grafik/Grafiktypen.cs');
  if (cs) {
    assert.match(cs, /Dick => 0\.70, Mittel => 0\.35, _ => 0\.18/);
    assert.deepEqual([pg.STIFT_MM.dick, pg.STIFT_MM.mittel, pg.STIFT_MM.duenn], [0.70, 0.35, 0.18]);
  }
  const m = (stift) => pg.primitivSvg(prim('linie', { punkte: [[0, 0], [1000, 0]] }, { stift }), ctx(50));
  assert.match(m('duenn'), /class="sd pl"[^>]*stroke-width="9"/);
  assert.match(m('mittel'), /class="sm pl"[^>]*stroke-width="17.5"/);
  assert.match(m('dick'), /class="sk pl"[^>]*stroke-width="35"/);
  assert.match(pg.primitivSvg(prim('linie', { punkte: [[0, 0], [1000, 0]] }, { stift: 'dick' }), ctx(100)), /stroke-width="70"/);
});

test('Mindestbreite ein Bildschirmpixel: CSS setzt stroke-width: max(Stift × --m, --minpx); grundriss.js stellt --minpx beim Zoomen', () => {
  const css = lies('css/ansichten.css');
  for (const [klasse, mm] of [['sd', '0.18'], ['sm', '0.35'], ['sk', '0.70']])
    assert.match(css, new RegExp(`\\.gr-svg \\.${klasse} \\{ stroke-width: max\\(calc\\(var\\(--m\\) \\* ${mm.replace('.', '\\.')}px\\), var\\(--minpx\\)\\); \\}`), klasse);
  assert.match(lies('js/ansichten/grundriss.js'), /setProperty\('--minpx'/);
  assert.match(lies('js/ansichten/grundriss.js'), /setProperty\('--m'/);
});

test('Linienarten als dasharray in Modell-mm – Längen wie im PDF (PdfZeichner.cs)', () => {
  assert.equal(pg.dasharray('voll', 50), null);
  assert.equal(pg.dasharray('strich', 50), '150 50');
  assert.equal(pg.dasharray('strichpunkt', 50), '300 50 25 50');
  assert.equal(pg.dasharray('punkt', 50), '25 50');
  assert.equal(pg.dasharray('strich', 100), '300 100');
  const cs = kernQuelle('Plan/PdfZeichner.cs');
  if (cs) {
    const muster = (name) => [...cs.match(new RegExp(`${name} = \\[([^\\]]*)\\]`))[1].matchAll(/[\d.]+/g)].map((m) => Number(m[0]));
    assert.deepEqual(pg.LINIENMUSTER.strich, muster('Strich'));
    assert.deepEqual(pg.LINIENMUSTER.strichpunkt, muster('Strichpunkt'));
    assert.deepEqual(pg.LINIENMUSTER.punkt, muster('Punkt'));
  }
  assert.match(pg.primitivSvg(prim('polylinie', { punkte: [[0, 0], [10, 0], [10, 10]] }, { linie: 'strichpunkt' }), ctx(50)), /stroke-dasharray="300 50 25 50"/);
  assert.doesNotMatch(pg.primitivSvg(prim('linie', { punkte: [[0, 0], [10, 0]] }), ctx(50)), /dasharray/);
});

test('Linie und Polylinie: Pfad in Modellkoordinaten, ohne Stift nichts, zu wenige Punkte nichts', () => {
  assert.match(pg.primitivSvg(prim('linie', { punkte: [[0, 0], [1000, 500]] }), ctx()), /d="M0 0L1000 500"/);
  assert.match(pg.primitivSvg(prim('polylinie', { punkte: [[0, 0], [10, 0], [10, 10.256]] }), ctx()), /d="M0 0L10 0L10 10.26"/);
  assert.equal(pg.primitivSvg(prim('linie', { punkte: [[0, 0], [1, 1]] }, { stift: null }), ctx()), '');
  assert.equal(pg.primitivSvg(prim('linie', { punkte: [[0, 0]] }), ctx()), '');
});

test('Text: Grösse in Papier-mm × Massstab, Ausrichtung → text-anchor, Winkel, fett, mehrzeilig, maskiert', () => {
  const t = (extra) => pg.primitivSvg(prim('text', { punkte: [[100, 200]], text: 'E.04', groesse: 2.5, ...extra }), ctx(50));
  assert.match(t({}), /font-size="125"/);                                   // 2.5 mm × 50
  assert.match(pg.primitivSvg(prim('text', { punkte: [[0, 0]], text: 'x', groesse: 2 }), ctx(100)), /font-size="200"/);
  assert.match(t({ ausrichtung: 'links' }), /text-anchor="start"/);
  assert.match(t({ ausrichtung: 'mitte' }), /text-anchor="middle"/);
  assert.match(t({ ausrichtung: 'rechts' }), /text-anchor="end"/);
  assert.match(t({}), /text-anchor="start"/);                               // Vorgabe wie der PDF-Zeichner: links
  assert.match(t({}), /transform="translate\(100 200\) scale\(1 -1\)"/);    // y-Spiegelung der Gruppe aufgehoben, Text steht aufrecht
  assert.match(t({ winkel: 90 }), /transform="translate\(100 200\) scale\(1 -1\) rotate\(-90\)"/);   // gegen den Uhrzeigersinn
  assert.doesNotMatch(t({ winkel: 0 }), /rotate/);
  assert.match(t({ fett: true }), /font-weight="bold"/);
  assert.doesNotMatch(t({}), /font-weight/);
  const mehr = t({ text: 'Bad\n12.00 m²' });
  assert.match(mehr, /<tspan x="0" dy="0">Bad<\/tspan><tspan x="0" dy="150">12\.00 m²<\/tspan>/);   // Zeilenabstand 1.2 × 125
  assert.match(t({ text: '<b>&"' }), />&lt;b&gt;&amp;&quot;</);
  assert.equal(pg.primitivSvg(prim('text', { punkte: [[0, 0]], text: '' }), ctx()), '');
});

test('Füllung: «schnitt» = Planfarbe, «papier» = Hintergrund, #rrggbb mit Deckkraft; Löcher bleiben leer', () => {
  const ring = [[0, 0], [1000, 0], [1000, 1000], [0, 1000]], loch = [[250, 250], [750, 250], [750, 750], [250, 750]];
  const p = (stil, extra = {}) => pg.primitivSvg(prim('polygon', { punkte: ring, ...extra }, stil), ctx());
  assert.match(p({ fuellung: 'schnitt', stift: null }), /class="pf"[^>]*stroke="none"/);
  assert.match(p({ fuellung: 'papier' }), /class="pp sd pl"/);
  assert.match(p({ fuellung: '#ecdff5', deckkraft: 0.35, stift: null }), /fill="#ecdff5" fill-opacity="0.35" stroke="none"/);
  assert.match(p({ fuellung: 'schnitt', deckkraft: 0.25, stift: null }), /class="pf"[^>]*fill-opacity="0.25"/);
  assert.doesNotMatch(p({ fuellung: '#ecdff5' }), /fill-opacity/);        // ohne Deckkraft deckend
  assert.match(p({ stift: null }), /fill="none" stroke="none"/);
  const mitLoch = p({ fuellung: 'schnitt' }, { loecher: [loch] });
  assert.match(mitLoch, /fill-rule="evenodd"/);
  assert.match(mitLoch, /d="M0 0L1000 0L1000 1000L0 1000ZM250 250L750 250L750 750L250 750Z"/);
  // Linienfarbe aus den Daten: Attribut statt Planfarbe
  assert.match(p({ farbe: '#8b5a2b' }), /stroke="#8b5a2b"/);
  assert.doesNotMatch(p({ farbe: '#8b5a2b' }), /class="[^"]*\bpl\b/);
  // kein Einschleusen über Farben
  assert.doesNotMatch(p({ fuellung: 'red" onload="x', farbe: '"><script>' }), /script|onload/);
});

test('Muster: Schraffur als SVG-pattern, Abstand mm Papier × Massstab, Winkel, im Ursprung verankert', () => {
  const ring = [[0, 0], [2000, 0], [2000, 1000], [0, 1000]];
  const svg = pg.planSvg({ massstab: 50, teile: [{ ebene: 'wand', primitive: [prim('polygon', { punkte: ring }, { stift: 'dick', fuellung: 'schnitt', muster: 'beton' })] }] }, schraffuren);
  const defs = svg.match(/<defs>(.*)<\/defs>/s)[1];
  assert.equal([...defs.matchAll(/<pattern /g)].length, 2);                  // Beton: 45° und 135°
  assert.match(defs, /width="60" height="60"/);                              // 1.2 mm × 50
  assert.match(defs, /patternTransform="rotate\(45\)"/);
  assert.match(defs, /patternTransform="rotate\(135\)"/);
  assert.match(defs, /patternUnits="userSpaceOnUse"/);
  assert.match(defs, /stroke-width="9"/);                                    // dünn
  assert.equal([...svg.matchAll(/fill="url\(#sch-beton-45-p-50\)"/g)].length, 1);
  assert.equal([...svg.matchAll(/fill="url\(#sch-beton-135-p-50\)"/g)].length, 1);
  // Reihenfolge wie im PDF: Füllung, Schraffur, Kontur
  const i = [svg.indexOf('class="pf"'), svg.indexOf('url(#sch-beton-45'), svg.indexOf('class="sk pl"')];
  assert.ok(i[0] >= 0 && i[0] < i[1] && i[1] < i[2], String(i));
  // Schraffur reagiert nicht auf Mausklicks (Treffer liegt am Teil)
  assert.match(svg, /class="ph"/);
});

test('Muster: Punkte, Vollton, Farbe aus den Daten, unbekannte Muster entfallen, gleiches Muster nur einmal definiert', () => {
  const ring = [[0, 0], [1000, 0], [1000, 1000], [0, 1000]];
  const poly = (muster, stil = {}) => ({ ebene: 'wand', primitive: [prim('polygon', { punkte: ring }, { stift: null, muster, ...stil })] });
  const putz = pg.planSvg({ massstab: 100, teile: [poly('putz')] }, schraffuren);
  assert.match(putz, /width="80" height="80"/);                              // 0.8 mm × 100
  assert.equal([...putz.matchAll(/<circle /g)].length, 4);                   // vier Ecken der Kachel
  assert.match(putz, /r="10"/);                                              // Punkt Ø 0.2 mm → r = 0.1 × 100
  assert.match(pg.planSvg({ massstab: 50, teile: [poly('voll')] }, schraffuren), /<path class="pf ph"/);
  const mitFarbe = pg.planSvg({ massstab: 50, teile: [poly('glas', { farbe: '#1e90ff' })] }, schraffuren);
  assert.match(mitFarbe, /<pattern id="sch-glas-0-1e90ff-50"/);
  assert.match(mitFarbe, /stroke="#1e90ff"/);
  const unbekannt = pg.planSvg({ massstab: 50, teile: [poly('gibtsnicht')] }, schraffuren);
  assert.doesNotMatch(unbekannt, /pattern|url\(/);
  const zweimal = pg.planSvg({ massstab: 50, teile: [poly('beton'), poly('beton')] }, schraffuren);
  assert.equal([...zweimal.matchAll(/<pattern /g)].length, 2);
  assert.equal([...zweimal.matchAll(/url\(#sch-beton-45/g)].length, 2);      // zwei Flächen, ein Muster
  assert.match(pg.planSvg({ massstab: 50, teile: [poly('beton')] }, schraffuren), /<defs>/);
  assert.doesNotMatch(pg.planSvg({ massstab: 50, teile: [poly('beton')] }), /url\(/);   // Schraffurliste (noch) nicht geladen
});

test('Kreis und Bogen: Radius in Modell-mm; Bogen gegen den Uhrzeigersinn, grosser Bogen, voller Kreis', () => {
  assert.match(pg.primitivSvg(prim('kreis', { punkte: [[100, 200]], radius: 37.5 }, { fuellung: 'papier' }), ctx()), /<circle class="pp sd pl" cx="100" cy="200" r="37.5"/);
  assert.equal(pg.primitivSvg(prim('kreis', { punkte: [[0, 0]], radius: 0 }), ctx()), '');
  assert.equal(pg.bogenPfad(0, 0, 100, 0, 90), 'M100 0A100 100 0 0 1 0 100');
  assert.equal(pg.bogenPfad(0, 0, 100, 0, 270), 'M100 0A100 100 0 1 1 0 -100');   // grosser Bogen
  assert.match(pg.bogenPfad(0, 0, 100, 350, 10), /^M98\.48 -17\.36A100 100 0 0 1 98\.48 17\.36$/);   // über 0° hinweg: 20°
  assert.match(pg.bogenPfad(50, 50, 10, 0, 360), /^M60 50A10 10 0 1 1 40 50A10 10 0 1 1 60 50$/);   // zwei Halbbögen
  assert.match(pg.primitivSvg(prim('bogen', { punkte: [[0, 0]], radius: 100, von: 0, bis: 90 }), ctx()), /<path class="sd pl" d="M100 0A100 100 0 0 1 0 100" fill="none"/);
  assert.equal(pg.primitivSvg(prim('bogen', { punkte: [[0, 0]], radius: 100, von: 0 }), ctx()), '');
});

test('Teile: mit Id anklickbar (<g data-id>, Trefferkonturen), ohne Id nicht; Ebene als Klasse; nurBildschirm wird gezeigt', () => {
  const linie = prim('linie', { punkte: [[0, 0], [1000, 0]] });
  const mit = pg.teilSvg({ id: 'abc-1', ebene: 'leitung', primitive: [linie] }, ctx());
  assert.match(mit, /^<g class="el e-leitung" data-id="abc-1">/);
  assert.match(mit, /<path class="th" d="M0 0L1000 0"\/><\/g>$/);
  const ohne = pg.teilSvg({ id: null, ebene: 'wand', primitive: [linie] }, ctx());
  assert.match(ohne, /^<g class="e-ohne e-wand">/);
  assert.doesNotMatch(ohne, /data-id|class="th"/);
  assert.match(pg.teilSvg({ id: 'x"><script>', ebene: 'wand', primitive: [linie] }, ctx()), /data-id="x&quot;&gt;&lt;script&gt;"/);
  assert.doesNotMatch(pg.teilSvg({ id: 'x', ebene: 'we"ird', primitive: [linie] }, ctx()), /we"ird/);
  // Bildschirmhilfen werden gezeigt (der PDF-Zeichner lässt sie weg, der Bildschirm nicht)
  const raum = pg.teilSvg({ id: 'r', ebene: 'raum', primitive: [prim('polygon', { punkte: [[0, 0], [1, 0], [1, 1]] }, { stift: null, fuellung: '#ecdff5', deckkraft: 0.35, nurBildschirm: true })] }, ctx());
  assert.match(raum, /fill="#ecdff5"/);
  assert.match(raum, /class="bf"/);   // Bildschirmfüllung: im Design über --gr-raum-deckung gedämpft
  assert.doesNotMatch(pg.primitivSvg(prim('polygon', { punkte: [[0, 0], [1, 0], [1, 1]] }, { fuellung: '#ecdff5' }), ctx()), /bf/);
  assert.match(lies('css/ansichten.css'), /\.gr-svg \.bf \{ opacity: var\(--gr-raum-deckung\); \}/);
  // Teile ohne zeichenbare Primitive ergeben nichts
  assert.equal(pg.teilSvg({ id: 'q', ebene: 'wand', primitive: [] }, ctx()), '');
  // Treffer: jede Kontur einmal, Polygone zusätzlich als Fläche
  const poly = pg.teilSvg({ id: 'p', ebene: 'raum', primitive: [prim('polygon', { punkte: [[0, 0], [1, 0], [1, 1]] }, { fuellung: 'schnitt' }), prim('polygon', { punkte: [[0, 0], [1, 0], [1, 1]] })] }, ctx());
  assert.equal([...poly.matchAll(/class="th fl"/g)].length, 1);
});

test('Planfarbe und Hintergrund kommen aus Klassen (Design), nie als feste Farbe im Markup', () => {
  const teile = [{ id: 'a', ebene: 'wand', primitive: [
    prim('polygon', { punkte: [[0, 0], [10, 0], [10, 10]] }, { stift: 'dick', fuellung: 'schnitt', muster: 'beton' }),
    prim('polygon', { punkte: [[0, 0], [10, 0], [10, 10]] }, { fuellung: 'papier' }),
    prim('linie', { punkte: [[0, 0], [10, 0]] }),
    prim('kreis', { punkte: [[0, 0]], radius: 5 }, { fuellung: 'schnitt' }),
    prim('text', { punkte: [[0, 0]], text: 'x' }),
  ] }];
  const svg = pg.planSvg({ massstab: 50, teile }, schraffuren);
  assert.doesNotMatch(svg, /#[0-9a-fA-F]{3,8}|rgb\(|black|white/);
  const css = lies('css/ansichten.css');
  assert.match(css, /\.gr-svg \{ --plan: var\(--gr-text\); --papier: var\(--zeichenflaeche\);/);   // folgt dem Design
  assert.match(css, /\.gr-svg \.pl \{ stroke: var\(--plan\); \}/);
  assert.match(css, /\.gr-svg \.pf \{ fill: var\(--plan\); \}/);
  assert.match(css, /\.gr-svg \.pp \{ fill: var\(--papier\); \}/);
});

test('Ungültige Primitive werfen nicht und ergeben keine NaN im Markup', () => {
  const schlecht = [null, {}, { art: 'polygon' }, { art: 'polygon', punkte: [[0, 0], [NaN, 1], [1, 1]] }, { art: 'kreis', punkte: [], radius: 5 },
    { art: 'linie', punkte: [[0, 0], ['x', 1]] }, { art: 'text', punkte: [[0, 0]], text: 'a', groesse: NaN, winkel: Infinity }, { art: 'unbekannt' }, { art: 'bogen', punkte: [[0, 0]], radius: 1, von: 'a', bis: 2 }];
  for (const p of schlecht) assert.doesNotMatch(String(pg.primitivSvg(p, ctx())), /NaN|undefined|Infinity/);
  assert.doesNotMatch(pg.planSvg({ massstab: 'x', teile: [{ ebene: 'wand', primitive: schlecht }] }), /NaN|undefined|Infinity/);
  assert.equal(pg.planSvg(null), '<defs></defs>');
});

test('Fangdaten und Begrenzung aus der Plangrafik', () => {
  const plan = { teile: [
    { id: 'w1', ebene: 'wand', fangpunkte: [[0, 0], [1000, 0]], fangkanten: [[[0, 100], [1000, 100]]], primitive: [] },
    { id: null, ebene: 'achse', fangpunkte: [[5, 5], [NaN, 1]], primitive: [] },
  ] };
  assert.deepEqual(pg.fangdatenVon(plan), {
    punkte: [{ p: [0, 0], id: 'w1' }, { p: [1000, 0], id: 'w1' }, { p: [5, 5], id: null }],
    kanten: [{ a: [0, 100], b: [1000, 100], id: 'w1' }],
  });
  assert.deepEqual(pg.fangdatenVon(null), { punkte: [], kanten: [] });
  assert.deepEqual(pg.begrenzungVon({ begrenzung: [-175, -175, 12175, 10175] }), { minx: -175, miny: -175, maxx: 12175, maxy: 10175 });
  const ohne = pg.begrenzungVon({ teile: [{ primitive: [prim('linie', { punkte: [[0, 0], [100, 50]] }), prim('kreis', { punkte: [[200, 200]], radius: 10 }), prim('text', { punkte: [[9999, 9999]], text: 'x' })] }] });
  assert.deepEqual(ohne, { minx: 0, miny: 0, maxx: 210, maxy: 210 });   // Text zählt nicht
  assert.equal(pg.begrenzungVon({ teile: [] }), null);
});

test('Zuordnung der Ansichtsarten: Schnitt und Ansicht → Plan-Ansicht, Planblatt → planblatt.js, 3D und Liste wie gehabt', () => {
  const q = lies('js/fenster.js');
  const abbildung = Object.fromEntries([...q.match(/const MODULE = \{([\s\S]*?)\};/)[1].matchAll(/['"]?(\w+)['"]?:\s*'([^']+)'/g)].map((m) => [m[1], m[2]]));
  assert.deepEqual(abbildung, { grundriss: './ansichten/grundriss.js', schnitt: './ansichten/grundriss.js', ansicht: './ansichten/grundriss.js',
    '3d': './ansichten/ansicht3d.js', liste: './ansichten/liste.js', planblatt: './ansichten/planblatt.js' });
  for (const pfad of new Set(Object.values(abbildung))) assert.ok(existsSync(path.join(wwwroot, 'js', pfad)), pfad);
  assert.match(q, /export function aktivePlanansicht\(\)/);
  assert.match(q, /a\.klasse !== 'Ansicht' && a\.klasse !== 'Planblatt'/);
  // alte Grafik-Logik des Browsers ist im Kern aufgegangen
  for (const alt of ['zeichnen2d.js', 'sinnbilder.js', 'bauteile3d.js', 'geometrie.js']) assert.equal(existsSync(path.join(hier, alt)), false, alt);
});

test('Werkzeug-Schnittstelle der Plan-Ansicht: alle Mitglieder aus der Spezifikation sind da', () => {
  const q = lies('js/ansichten/grundriss.js');
  for (const m of ['get art()', 'get ansicht()', 'get massstab()', 'zuModell(clientX, clientY)', 'zuBildschirm(p)', 'pxJeMm()', 'fangdaten()', 'werkzeugSchicht,', 'setzeWerkzeug(w)', 'get werkzeug()',
    'aktualisieren(ereignis)', 'ansichtGeaendert,', 'auswahlGeaendert(id)', 'zoomAlles,', 'zerstoeren()'])
    assert.ok(q.includes(m), m);
  // Ereignisweitergabe an das Werkzeug: Maus, Doppelklick, Tastatur
  for (const e of ['pointerdown', 'pointermove', 'pointerup', 'dblclick', 'keydown']) assert.match(q, new RegExp(`anWerkzeug\\('${e}'`), e);
  assert.match(q, /werkzeug\.ereignis\(ereignisArt, e, instanz\)/);
  assert.match(q, /scale\(1 -1\)"><g class="gr-plan"><\/g><g class="gr-werkzeug"><\/g>/);   // werkzeugSchicht in Modellkoordinaten, über allem
});

// ---------- Körper des Kerns → three ----------
const box = (g) => new THREE.Box3().setFromObject(g);
const nahe = (a, b, eps = 1e-6) => assert.ok(Math.abs(a - b) < eps, `${a} ≠ ${b}`);
/** Fläche der nach oben zeigenden Dreiecke (Deckfläche) einer Geometrie in m². */
function deckflaeche(geom) {
  const g = geom.index ? geom.toNonIndexed() : geom;
  const p = g.attributes.position;
  let summe = 0;
  for (let i = 0; i < p.count; i += 3) {
    const a = new THREE.Vector3().fromBufferAttribute(p, i), b = new THREE.Vector3().fromBufferAttribute(p, i + 1), c = new THREE.Vector3().fromBufferAttribute(p, i + 2);
    const n = new THREE.Vector3().crossVectors(b.clone().sub(a), c.clone().sub(a));
    if (n.length() > 0 && n.y / n.length() > 0.99) summe += n.length() / 2;
  }
  return summe;
}

test('3D: Koordinatenabbildung x = x, y = Höhe, z = −y (Meter)', () => {
  const v = k3.vek([1000, 2000, 3000]);
  assert.deepEqual([v.x, v.y, v.z], [1, 3, -2]);
});

test('3D: Quader – Mitte, Kantenlängen, Drehung um z', () => {
  const g = k3.quaderGeometrie({ art: 'quader', mitte: [6000, 0, -1350], masse: [12350, 350, 2700], drehung: 0 });
  g.computeBoundingBox();
  const b = g.boundingBox, c = b.getCenter(new THREE.Vector3()), s = b.getSize(new THREE.Vector3());
  [c.x, c.y, c.z].forEach((x, i) => nahe(x, [6, -1.35, 0][i]));
  [s.x, s.y, s.z].forEach((x, i) => nahe(x, [12.35, 2.7, 0.35][i]));      // x, Höhe, Tiefe (= ly)
  // 90° gegen den Uhrzeigersinn: Länge liegt dann entlang y (Modell) = z (three)
  const d = k3.quaderGeometrie({ art: 'quader', mitte: [1000, 2000, 500], masse: [4000, 1000, 2000], drehung: 90 });
  d.computeBoundingBox();
  const sd = d.boundingBox.getSize(new THREE.Vector3()), cd = d.boundingBox.getCenter(new THREE.Vector3());
  [sd.x, sd.y, sd.z].forEach((x, i) => nahe(x, [1, 2, 4][i]));
  [cd.x, cd.y, cd.z].forEach((x, i) => nahe(x, [1, 0.5, -2][i]));
  // 30°: eine Ecke (+lx/2, +ly/2) liegt bei Modell (x cos − y sin, x sin + y cos)
  const e = k3.quaderGeometrie({ art: 'quader', mitte: [0, 0, 0], masse: [2000, 1000, 1000], drehung: 30 });
  const p = e.attributes.position;
  const ecken = [...Array(p.count).keys()].map((i) => [p.getX(i), -p.getZ(i)]);
  const w = 30 * Math.PI / 180, soll = [1 * Math.cos(w) - 0.5 * Math.sin(w), 1 * Math.sin(w) + 0.5 * Math.cos(w)];
  assert.ok(ecken.some(([x, y]) => Math.abs(x - soll[0]) < 1e-6 && Math.abs(y - soll[1]) < 1e-6), 'Drehsinn');
  assert.equal(k3.quaderGeometrie({ art: 'quader', mitte: [0, 0, 0], masse: [0, 1, 1] }), null);
  assert.equal(k3.quaderGeometrie({ art: 'quader', masse: [1, 1, 1] }), null);
});

test('3D: Prisma – Extrusion von unten bis oben, Löcher bleiben leer', () => {
  const t = { art: 'prisma', grundriss: [[0, 6000], [4000, 6000], [4000, 10000], [0, 10000]], unten: 2900, oben: 5400 };
  const g = k3.prismaGeometrie(t);
  g.computeBoundingBox();
  const b = g.boundingBox;
  [b.min.x, b.max.x, b.min.y, b.max.y, b.min.z, b.max.z].forEach((x, i) => nahe(x, [0, 4, 2.9, 5.4, -10, -6][i]));
  nahe(deckflaeche(g), 16);
  const mitLoch = k3.prismaGeometrie({ ...t, loecher: [[[1000, 7000], [2000, 7000], [2000, 8000], [1000, 8000]]] });
  nahe(deckflaeche(mitLoch), 15);                                         // 16 m² − 1 m² Loch
  assert.equal(k3.prismaGeometrie({ ...t, oben: 2900 }), null);
  assert.equal(k3.prismaGeometrie({ ...t, grundriss: [[0, 0], [1, 1]] }), null);
});

test('3D: Zylinder zwischen von und nach, Radius, 32 Segmente', () => {
  const waagrecht = k3.zylinderGeometrie({ art: 'zylinder', von: [0, 0, 1000], nach: [2000, 0, 1000], radius: 50 });
  waagrecht.computeBoundingBox();
  const b = waagrecht.boundingBox;
  [b.min.x, b.max.x, b.min.y, b.max.y, b.min.z, b.max.z].forEach((x, i) => nahe(x, [0, 2, 0.95, 1.05, -0.05, 0.05][i]));
  const senkrecht = k3.zylinderGeometrie({ art: 'zylinder', von: [500, 500, 0], nach: [500, 500, 3000], radius: 20 });
  senkrecht.computeBoundingBox();
  [senkrecht.boundingBox.min.y, senkrecht.boundingBox.max.y].forEach((x, i) => nahe(x, [0, 3][i]));
  nahe(senkrecht.boundingBox.max.x - senkrecht.boundingBox.min.x, 0.04);
  assert.equal(k3.RUND, 32);
  assert.equal(new THREE.CylinderGeometry(1, 1, 1, k3.RUND).parameters.radialSegments, 32);
  // schräg: Länge stimmt
  const schraeg = k3.zylinderGeometrie({ art: 'zylinder', von: [0, 0, 0], nach: [3000, 4000, 0], radius: 10 });
  schraeg.computeBoundingBox();
  nahe(schraeg.boundingBox.max.x - schraeg.boundingBox.min.x, 3 + 0.0, 0.03);
  assert.equal(k3.zylinderGeometrie({ art: 'zylinder', von: [0, 0, 0], nach: [0, 0, 0], radius: 10 }), null);
});

test('3D: Kugel um die Mitte', () => {
  const g = k3.kugelGeometrie({ art: 'kugel', mitte: [10200, 9800, 3200], radius: 65 });
  g.computeBoundingBox();
  const c = g.boundingBox.getCenter(new THREE.Vector3()), s = g.boundingBox.getSize(new THREE.Vector3());
  [c.x, c.y, c.z].forEach((x, i) => nahe(x, [10.2, 3.2, -9.8][i]));
  nahe(s.x, 0.13);
  nahe(s.y, 0.13);
  assert.equal(k3.kugelGeometrie({ art: 'kugel', mitte: [0, 0, 0], radius: 0 }), null);
  assert.equal(k3.teilGeometrie({ art: 'unbekannt' }), null);
});

/** CSS-Variablen für cssVar nachbilden (Design hell oder dunkel). */
function mitDesign(design, vars, fn) {
  globalThis.document = { documentElement: { getAttribute: () => design } };
  globalThis.getComputedStyle = () => ({ getPropertyValue: (n) => vars[n] ?? '' });
  try { return fn(); } finally { delete globalThis.document; delete globalThis.getComputedStyle; }
}

test('3D: Farben – Teilfarbe, sonst Materialfarbe aus --d3-<material>, sonst Vorgabe; Deckkraft aus dem Teil oder dem Material', () => {
  const vars = { '--d3-wand': '#102030', '--d3-wand-deckung': '0.5', '--d3-apparat': '#405060', '--d3-kante': '#aabbcc' };
  mitDesign('dunkel', vars, () => {
    assert.equal(k3.teilFarbe({ material: 'wand' }), '#102030');
    assert.equal(k3.teilFarbe({ material: 'wand', farbe: '#2e8b3a' }), '#2e8b3a');                 // Farbe des Kerns gewinnt
    assert.equal(k3.teilFarbe({ material: 'apparat' }), '#405060');
    assert.equal(k3.teilFarbe({ material: 'rohr' }), '#9aa3ae');                                     // keine Variable → Vorgabe (dunkel)
    assert.equal(k3.teilFarbe({ material: 'formteil' }), '#9aa3ae');                                 // Alias → rohr
    assert.equal(k3.teilFarbe({ material: 'gibtsnicht' }), '#8a929e');                               // sonst
    assert.equal(k3.teilFarbe({ material: 'wand', farbe: 'red" x="' }), '#102030');                  // ungültige Farbe wird ignoriert
    assert.equal(k3.teilDeckkraft({ material: 'wand' }), 0.5);
    assert.equal(k3.teilDeckkraft({ material: 'glas', deckkraft: 0.35 }), 0.35);
    assert.equal(k3.teilDeckkraft({ material: 'raum', deckkraft: 0.15 }), 0.15);
    assert.equal(k3.teilDeckkraft({ material: 'tuer' }), 1);
  });
  mitDesign('hell', {}, () => {
    assert.equal(k3.teilFarbe({ material: 'wand' }), '#cfd3d8');
    assert.equal(k3.teilFarbe({ material: 'rohr' }), '#777777');
    assert.equal(k3.teilDeckkraft({ material: 'wand' }), 0.28);
  });
  // alle Materialien des Kerns haben eine Vorgabe in beiden Designs
  for (const d of ['hell', 'dunkel']) mitDesign(d, {}, () => {
    for (const m of ['wand', 'decke', 'raum', 'glas', 'tuer', 'rohr', 'apparat', 'geraet', 'armatur', 'sonst', 'formteil'])
      assert.match(k3.teilFarbe({ material: m }), /^#[0-9a-f]{6}$/i, `${d}/${m}`);
  });
});

test('3D: Körper – Gruppe mit Id, Ebene, Vorrang; Material je Farbe/Deckkraft nur einmal; Kanten nur bei deckenden Teilen', () => {
  const k = { id: 'k1', ebene: 'leitung', teile: [
    { art: 'zylinder', von: [0, 0, 0], nach: [1000, 0, 0], radius: 15, farbe: '#2e8b3a', material: 'rohr' },
    { art: 'zylinder', von: [1000, 0, 0], nach: [1000, 1000, 0], radius: 15, farbe: '#2e8b3a', material: 'rohr' },
    { art: 'kugel', mitte: [1000, 0, 0], radius: 15, farbe: '#2e8b3a', material: 'rohr' },
    { art: 'quader', mitte: [0, 0, 0], masse: [100, 100, 100], material: 'apparat' },
    { art: 'prisma', grundriss: [[0, 0], [1, 0], [1, 1]], unten: 0, oben: 1, material: 'raum', deckkraft: 0.15 },
    { art: 'kaputt' },
  ] };
  const g = mitDesign('hell', {}, () => k3.baueKoerper(k));
  assert.equal(g.userData.id, 'k1');
  assert.equal(g.userData.ebene, 'leitung');
  assert.equal(g.userData.vorrang, k3.VORRANG.leitung);
  assert.equal(g.userData.materialien.length, 3);                                   // grün, Apparat, Raum
  const meshes = g.children.filter((c) => c.isMesh), kanten = g.children.filter((c) => c.userData.kante);
  assert.equal(meshes.length, 5);                                                   // das kaputte Teil entfällt
  assert.equal(kanten.length, 1);                                                   // nur der Quader (Raum ist zu durchsichtig, Rohre haben keine)
  assert.ok(meshes.every((m) => m.userData.id === 'k1'));
  const raum = g.userData.materialien.find((m) => m.opacity === 0.15);
  assert.ok(raum.transparent && raum.depthWrite === false);
  assert.equal(g.userData.materialien.find((m) => m.opacity === 1).transparent, false);
  // Kanten lassen sich nie anklicken
  const strahl = new THREE.Raycaster(new THREE.Vector3(0, 5, 0), new THREE.Vector3(0, -1, 0));
  assert.equal(strahl.intersectObject(kanten[0]).length, 0);
  k3.freigeben(g);
  assert.equal(k3.baueKoerper({ id: 'leer', ebene: 'wand', teile: [] }), null);
  assert.equal(k3.baueKoerper({ id: 'x', ebene: 'wand', teile: [{ art: 'quader' }] }), null);
  assert.equal(k3.baueKoerper({ id: 'y' }), null);
});

test('3D: Hervorheben und Zurücksetzen der Farbe', () => {
  const g = mitDesign('hell', {}, () => k3.baueKoerper({ id: 'w', ebene: 'wand', teile: [{ art: 'quader', mitte: [0, 0, 0], masse: [1000, 100, 1000], material: 'wand' }] }));
  const m = g.userData.materialien[0];
  const vorher = m.color.getHex();
  k3.hervorheben(g, 'auswahl', new THREE.Color('#1e90ff'));
  assert.notEqual(m.color.getHex(), vorher);
  assert.ok(m.emissive.getHex() !== 0);
  k3.hervorheben(g, null);
  assert.equal(m.color.getHex(), vorher);
  assert.equal(m.emissive.getHex(), 0);
  assert.doesNotThrow(() => k3.freigeben(g));
});

test('3D: Vorrang beim Anklicken – Haustechnik vor Wand und Decke, diese vor Räumen', () => {
  assert.ok(k3.VORRANG.leitung > k3.VORRANG.wand && k3.VORRANG.apparat > k3.VORRANG.wand);
  assert.ok(k3.VORRANG.wand > k3.VORRANG.raum && k3.VORRANG.decke > k3.VORRANG.raum);
  assert.equal(k3.vorrangVon('gibtsnicht'), k3.VORRANG.sonst);
});

test('3D: Nachladen – bei Wand, Tür, Fenster, Decke alle Körper neu, sonst nur die geänderten; URL', () => {
  const klassen = { a: 'Rohr', b: 'Wand', c: 'Tuer', d: 'Fenster', e: 'Decke', f: 'Raum', g: 'WC' };
  const klasseVon = (id) => klassen[id] ?? null;
  assert.equal(k3.brauchtAlleKoerper(['a', 'f', 'g'], klasseVon), false);
  for (const id of ['b', 'c', 'd', 'e']) assert.equal(k3.brauchtAlleKoerper(['a', id], klasseVon), true, id);
  // gelöschte Elemente kennt der Zustand nicht mehr: die bisherige Körperebene entscheidet
  assert.equal(k3.brauchtAlleKoerper(['x'], klasseVon, (id) => (id === 'x' ? 'wand' : null)), true);
  assert.equal(k3.brauchtAlleKoerper(['x'], klasseVon, () => 'oeffnung'), true);
  assert.equal(k3.brauchtAlleKoerper(['x'], klasseVon, () => 'leitung'), false);
  assert.equal(k3.brauchtAlleKoerper([], klasseVon), false);
  assert.equal(k3.koerperUrl(null), '/api/koerper');
  assert.equal(k3.koerperUrl([]), '/api/koerper');
  assert.equal(k3.koerperUrl(['a', 'b c']), '/api/koerper?ids=a,b%20c');
  assert.equal(k3.koerperUrl(Array.from({ length: k3.MAX_IDS + 1 }, (_, i) => String(i))), '/api/koerper');
});

test('3D: ansicht3d.js liest nur die Körper des Kerns (keine eigene Geometrie aus Elementen)', () => {
  const q = lies('js/ansichten/ansicht3d.js');
  assert.match(q, /holen\(koerperUrl\(ids\)\)/);
  assert.doesNotMatch(q, /bauteile3d|sinnbilder|geometrie\.js|alleElemente/);
  assert.match(q, /hlks-design/);                                                   // Designwechsel
});

// ---------- Planblatt ----------
test('Planblatt: Adresse des PDFs; Aktionen im Register Ansicht, Gruppe Planblätter', async () => {
  const pb = await import('./planblatt.js');
  assert.equal(pb.pdfUrl('PB-1'), '/api/plan/PB-1.pdf');
  assert.equal(pb.pdfUrl('PB 1/ä'), '/api/plan/PB%201%2F%C3%A4.pdf');
  const q = lies('js/ansichten/planblatt.js');
  assert.match(q, /<iframe/);
  assert.match(q, /ENTPRELLUNG = 1000/);
  assert.doesNotMatch(q, /three|svg/i);
});

test('Planblatt-Aktionen: nur über registrieren(); Ansicht platzieren nur mit Blatt und freier Ansicht; PDF öffnet neues Fenster', async () => {
  const geoeffnet = [];
  globalThis.localStorage = { getItem: () => null, setItem() {} };
  globalThis.window = { open: (...a) => geoeffnet.push(a), addEventListener() {}, dispatchEvent() {} };
  globalThis.document = { getElementById: () => null, createElement: () => ({}), addEventListener() {}, removeEventListener() {} };
  try {
    const z = await import('../zustand.js');
    const reg = await import('../oberflaeche/register.js');
    await import('../oberflaeche/aktionen-plaene.js');
    const a = (id) => reg.aktionen.get(id);
    for (const id of ['plan.anlegen', 'plan.platzieren', 'plan.pdf']) assert.deepEqual([a(id).register, a(id).gruppe], ['ansicht', 'Planblätter'], id);
    assert.equal(a('plan.oeffnen').register, undefined);                             // nur Kontextmenü
    const el = (id, kz, klasse, extra = {}) => z.zustand.elemente.set(id, { id, kennzeichen: kz, klasse, werte: {}, ...extra }) && z.zustand.nachKennzeichen.set(kz.toLowerCase(), id);
    z.zustand.elemente.clear(); z.zustand.nachKennzeichen.clear();
    assert.equal(reg.istAktiv(a('plan.platzieren')), false);                         // kein Blatt
    assert.equal(reg.istAktiv(a('plan.pdf')), false);
    assert.equal(reg.istAktiv(a('plan.anlegen')), true);
    el('b1', 'PB-1', 'Planblatt', { werte: { nummer: '2317-101', titel: 'Grundriss EG' } });
    assert.equal(reg.istAktiv(a('plan.platzieren')), false);                         // keine Ansicht
    el('g1', 'AN-GR-EG', 'Ansicht', { werte: { art: 'grundriss' } });
    el('d1', 'AN-3D', 'Ansicht', { werte: { art: '3d' } });                           // 3D kommt nicht auf ein Blatt
    el('v1', 'VL-1', 'Ansicht', { werte: { art: 'grundriss', vorlage: true } });      // Vorlagen nicht
    assert.equal(reg.istAktiv(a('plan.platzieren')), true);
    el('f1', 'AF-1', 'Ansichtsfenster', { bezuege: { ansicht: 'AN-GR-EG' } });        // schon platziert
    assert.equal(reg.istAktiv(a('plan.platzieren')), false);
    assert.match(reg.hinweisText(a('plan.platzieren')), /stehen schon/);
    // PDF: ein einziges Blatt wird ohne Rückfrage geöffnet
    assert.equal(reg.istAktiv(a('plan.pdf')), true);
    await reg.ausfuehren('plan.pdf', { planblatt: 'PB-1' });
    assert.deepEqual(geoeffnet[0], ['/api/plan/PB-1.pdf', '_blank', 'noopener']);
    z.zustand.elemente.clear(); z.zustand.nachKennzeichen.clear();
  } finally { delete globalThis.localStorage; delete globalThis.window; delete globalThis.document; }
});

// ---------- Liste ----------
const raumliste = {
  ableitung: 'Raumliste', projekt: 'MFH', modellStand: { pruefsumme: 'abc123', pakete: 175 }, erzeugt: '2026-10-17 18:04',
  anzahl: 2, flaecheTotal_m2: 37, summenJeGeschoss: [{ geschoss: 'UG', raeume: 1, flaeche_m2: 25 }, { geschoss: 'EG', raeume: 1, flaeche_m2: 12 }],
  zeilen: [
    { raum: 'U.10', nummer: 'U.10', name: 'Technik', geschoss: 'UG', nutzung: 'Technik', flaeche_m2: 25, hoehe_m: 2.4, volumen_m3: 60, anzahlEndgeraete: 0, endgeraete: [], systeme: [] },
    { raum: 'U.2', nummer: 'U.2', name: 'Bad', geschoss: 'UG', nutzung: 'Bad', flaeche_m2: 12.5, hoehe_m: 2.5, volumen_m3: 31.25, anzahlEndgeraete: 2, endgeraete: ['WC-1 (WC)', 'WT-1 (Waschtisch)'], systeme: ['KW', 'SW'] },
  ],
};

test('Raumliste: Spaltenreihenfolge, deutsche Titel, «nummer» entfällt wenn gleich dem Raum', () => {
  const a = fmt.aufbereiten('raumliste', raumliste);
  assert.equal(a.titel, 'Raumliste');
  assert.deepEqual(a.stand, { pruefsumme: 'abc123', pakete: 175 });
  const t = a.tabellen[0];
  assert.equal(t.schluessel, 'raum');
  assert.deepEqual(t.spalten.map((s) => s.titel), ['Raum', 'Name', 'Geschoss', 'Nutzung', 'Fläche m²', 'Höhe m', 'Volumen m³', 'Anzahl Endgeräte', 'Endgeräte', 'Systeme']);
  assert.match(a.zusammenfassung[0], /2 Räume · Gesamtfläche 37,00 m²/);
  assert.match(a.zusammenfassung[1], /UG: 1 Raum, 25,00 m²/);
});

test('Raumliste: «nummer» bleibt, wenn es vom Raum abweicht', () => {
  const kopie = structuredClone(raumliste);
  kopie.zeilen[0].nummer = 'X';
  const t = fmt.aufbereiten('raumliste', kopie).tabellen[0];
  assert.ok(t.spalten.some((s) => s.schluessel === 'nummer'));
});

test('Werte mit Komma, Listen mit Komma getrennt', () => {
  assert.equal(fmt.formatWert('flaeche_m2', 12.5), '12,50');
  assert.equal(fmt.formatWert('flaeche_m2', 25), '25,00');
  assert.equal(fmt.formatWert('anzahl', 3), '3');
  assert.equal(fmt.formatWert('x', 2.456), '2,46');
  assert.equal(fmt.formatWert('endgeraete', ['A', 'B']), 'A, B');
  assert.equal(fmt.formatWert('x', null), '');
  assert.equal(fmt.formatWert('x', true), 'ja');
  assert.equal(fmt.formatWert('je', { Dusche: 2, WC: 1 }), 'Dusche: 2, WC: 1');
});

test('Sortieren: natürlich, numerisch, absteigend, leere am Ende', () => {
  const z = [{ k: 'U.10', n: 5 }, { k: 'U.2', n: null }, { k: 'U.1', n: 30 }];
  assert.deepEqual(fmt.sortiere(z, 'k').map((r) => r.k), ['U.1', 'U.2', 'U.10']);
  assert.deepEqual(fmt.sortiere(z, 'n').map((r) => r.n), [5, 30, null]);
  assert.deepEqual(fmt.sortiere(z, 'n', true).map((r) => r.n), [30, 5, null]);
  assert.deepEqual(z.map((r) => r.k), ['U.10', 'U.2', 'U.1']);   // Eingabe bleibt unverändert
});

test('Strangliste: Titel und Spalten, Schlüssel «strang»', () => {
  const a = fmt.aufbereiten('strangliste', { ableitung: 'Strangliste', anzahl: 1, modellStand: { pruefsumme: 'x', pakete: 1 },
    zeilen: [{ strang: 'KW-S1', name: 'Steig', art: 'steigstrang', system: 'KW', systemtyp: 'Trinkwasser kalt', geschosse: ['UG', 'EG'], bauteile: 24, leitungslaenge_m: 25.39,
      groesse: 'NW 15–25', endgeraeteDirekt: ['WC'], anzahlDirekt: 1, anzahlDirektJeKlasse: { WC: 1 }, anzahlGesamt: 1, datenpunkte: [] }] });
  const t = a.tabellen[0];
  assert.equal(t.schluessel, 'strang');
  assert.equal(t.spalten[0].titel, 'Strang');
  assert.ok(!t.spalten.some((s) => s.schluessel === 'anzahlDirektJeKlasse'));
  assert.equal(fmt.formatWert('leitungslaenge_m', 25.39), '25,39');
  assert.equal(a.zusammenfassung[0], '1 Strang');
});

test('Mengen: zwei Tabellen ohne Klick-Schlüssel', () => {
  const a = fmt.aufbereiten('mengen', { ableitung: 'Mengen', leitungslaengeTotal_m: 120.19, leitungen: [{ system: 'KW', klasse: 'Rohr', material: 'MV', groesse: 'NW 15', stueck: 15, laenge_m: 30.1 }],
    bauteile: [{ klasse: 'WC', gewerk: 'sanitaer', stueck: 4, bkp: '251', bkpHinweis: 'zu prüfen' }] });
  assert.equal(a.tabellen.length, 2);
  assert.deepEqual(a.tabellen.map((t) => t.titel), ['Leitungen', 'Bauteile']);
  assert.ok(a.tabellen.every((t) => t.schluessel === null));
  assert.match(a.zusammenfassung[0], /120,19 m/);
});

// Modul 3, B4: Materialliste – Gruppen Leitungen / Formteile wie bei den Mengen
const materialliste = {
  ableitung: 'Materialliste', projekt: 'MFH', modellStand: { pruefsumme: 'abc', pakete: 12 }, erzeugt: '2026-10-18 12:00',
  filter: { system: 'KW', strang: null, geschoss: null },
  leitungen: [{ serie: 'Testrohrserie (fiktiv)', groesse: 'DN 20', stueck: 2, laenge_m: 4.45 }, { serie: 'ohne Serie', groesse: 'DN 100', stueck: 1, laenge_m: 4 }],
  leitungslaengeTotal_m: 8.45,
  formteile: [{ familie: 'Testbogen (fiktiv)', typ: 'DN 20', klasse: 'Bogen', winkel: 45, stueck: 1 }, { familie: 'Testbogen (fiktiv)', typ: 'DN 20', klasse: 'Bogen', winkel: 90, stueck: 2 },
    { familie: 'Test-T-Stück (fiktiv)', typ: 'DN 20', klasse: 'TStueck', winkel: null, stueck: 2 }],
  formteileTotal: 5, hinweis: '1 Leitung hat keine Serie (Prüfung P18): sie steht unter «ohne Serie».',
};

test('Materialliste: zwei Tabellen (Leitungen, Formteile) ohne Klick-Schlüssel, Spalten und Zusammenfassung', () => {
  const a = fmt.aufbereiten('materialliste', materialliste);
  assert.equal(a.titel, 'Materialliste');
  assert.deepEqual(a.tabellen.map((t) => t.titel), ['Leitungen', 'Formteile']);
  assert.ok(a.tabellen.every((t) => t.schluessel === null));
  assert.deepEqual(a.tabellen[0].spalten.map((s) => s.titel), ['Serie', 'Grösse', 'Stück', 'Länge m']);
  assert.deepEqual(a.tabellen[0].spalten.map((s) => s.art), ['text', 'text', 'zahl', 'zahl']);
  assert.deepEqual(a.tabellen[1].spalten.map((s) => s.titel), ['Familie', 'Typ', 'Klasse', 'Winkel °', 'Stück']);
  assert.equal(a.tabellen[0].zeilen.length, 2);
  assert.equal(a.tabellen[1].zeilen.length, 3);
  assert.match(a.zusammenfassung.join('\n'), /Leitungslänge total 8,45 m/);
  assert.match(a.zusammenfassung.join('\n'), /5 Formteile/);
  assert.match(a.zusammenfassung.join('\n'), /Filter: system KW/);
  assert.match(a.zusammenfassung.join('\n'), /keine Serie/);
  // Werte: Länge mit zwei Stellen und Komma, Winkel ganzzahlig, fehlender Winkel leer
  assert.equal(fmt.formatWert('laenge_m', 4.45), '4,45');
  assert.equal(fmt.formatWert('laenge_m', 4), '4,00');
  assert.equal(fmt.formatWert('winkel', 90), '90');
  assert.equal(fmt.formatWert('winkel', null), '');
  // sortieren nach Länge absteigend
  assert.deepEqual(fmt.sortiere(a.tabellen[0].zeilen, 'laenge_m', true).map((z) => z.groesse), ['DN 20', 'DN 100']);
});

test('Materialliste: leere oder fehlende Antwort wirft nicht, ohne Filter und Hinweis keine Zusatzzeilen', () => {
  assert.equal(fmt.aufbereiten('materialliste', {}).tabellen.length, 2);
  assert.equal(fmt.aufbereiten('materialliste', null).tabellen[1].zeilen.length, 0);
  const a = fmt.aufbereiten('materialliste', { ableitung: 'Materialliste', filter: { system: null, strang: null, geschoss: null }, leitungen: [], formteile: [], leitungslaengeTotal_m: 0, formteileTotal: 0 });
  assert.deepEqual(a.zusammenfassung, ['Leitungslänge total 0,00 m', '0 Formteile']);
});

test('Strangliste: Längen je Serie und Grösse werden lesbar angezeigt', () => {
  const laengen = [{ serie: 'Testrohrserie (fiktiv)', groesse: 'DN 20', laenge_m: 4.45 }, { serie: 'ohne Serie', groesse: 'DN 100', laenge_m: 4 }];
  assert.equal(fmt.formatWert('laengen', laengen), 'DN 20 (Testrohrserie (fiktiv)): 4,45 m; DN 100 (ohne Serie): 4,00 m');
  const a = fmt.aufbereiten('strangliste', { ableitung: 'Strangliste', anzahl: 1, zeilen: [{ strang: 'ST-1', leitungslaenge_m: 8.45, laengen, bauteile: 3 }] });
  const titel = a.tabellen[0].spalten.map((s) => s.titel);
  assert.ok(titel.indexOf('Längen je Serie und Grösse') > titel.indexOf('Leitungslänge m'));
  assert.equal(fmt.formatWert('laengen', []), '');
});

test('Liste: die Ansichtsart liste kennt materialliste (liste.js)', () => {
  const quelle = lies('js/ansichten/liste.js');
  assert.match(quelle, /NAMEN_MODUL3 = \['materialliste'\]/);
  assert.match(quelle, /NAMEN = \['raumliste', 'strangliste', 'mengen', 'raumgrenzen', 'huellbauteile'\]/);   // unverändert (huelle.test.mjs prüft das ebenfalls)
  assert.match(quelle, /\['raumliste', 'mengen', 'raumgrenzen', 'huellbauteile', 'materialliste'\]\.includes\(n\)/);   // Geschossfilter der Ansicht
});

test('Unbekannte Liste: erste Objektliste wird tabelliert, Titel humanisiert', () => {
  const a = fmt.aufbereiten('flaechen_neu', { eintraege: [{ a_wert_m2: 1.5, anzahlRaeume: 2, text: 'x' }] });
  assert.equal(a.titel, 'Flaechen neu');
  const t = a.tabellen[0];
  assert.deepEqual(t.spalten.map((s) => s.titel), ['A wert m²', 'Anzahl Raeume', 'Text']);
  assert.deepEqual(t.spalten.map((s) => s.art), ['zahl', 'zahl', 'text']);
});

test('Leere oder fehlerhafte Antwort wirft nicht', () => {
  assert.equal(fmt.aufbereiten('raumliste', null).tabellen[0].zeilen.length, 0);
  assert.equal(fmt.aufbereiten('mengen', {}).tabellen.length, 2);
  assert.equal(fmt.aufbereiten('raumliste', 'text').tabellen[0].zeilen.length, 0);
});


// ---------- Echte Daten des Kerns (nur wenn ein Testserver mit dem Beispielprojekt läuft) ----------
async function findeServer() {
  // Nur ein ausdrücklich genannter Testserver (19.10.2026): früher wurden 5134/5131/5130 von selbst abgefragt – darunter
  // der Desktop-Server mit einem echten Projekt; das Ergebnis hing dann vom Rechner ab (Befund G1/R, stand_pruefen.ps1)
  const kandidaten = [process.env.HLKS_TESTSERVER, process.env.HLKS_TEST_SERVER].filter(Boolean);
  for (const basis of kandidaten) {
    try {
      const r = await fetch(basis + '/api/koerper', { signal: AbortSignal.timeout(2500) });
      if (r.ok && Array.isArray(await r.json())) return basis;
    } catch { /* nächster */ }
  }
  return null;
}
const server = await findeServer();
const holeJson = async (pfad) => (await fetch(server + pfad)).json();

test('Echte Daten: jede Plangrafik der Grundrisse lässt sich zeichnen, ohne NaN und mit Ids für anklickbare Teile', { skip: server ? false : 'kein Testserver erreichbar' }, async () => {
  const modell = await holeJson('/api/modell');
  const sch = pg.schraffurMap(await holeJson('/api/schraffuren'));
  const ansichten = modell.elemente.filter((e) => e.klasse === 'Ansicht' && ['grundriss', 'schnitt', 'ansicht'].includes(e.werte?.art));
  assert.ok(ansichten.length > 0);
  for (const a of ansichten) {
    const plan = await holeJson('/api/plangrafik/' + encodeURIComponent(a.kennzeichen));
    assert.equal(plan.ansicht, a.kennzeichen);
    const svg = pg.planSvg(plan, sch);
    assert.doesNotMatch(svg, /NaN|undefined|Infinity/, a.kennzeichen);
    const mitId = plan.teile.filter((t) => t.id && t.primitive?.length);
    for (const t of mitId.slice(0, 50)) assert.ok(svg.includes(`data-id="${t.id}"`), t.id);
    assert.equal([...svg.matchAll(/<g class="e-ohne/g)].length, plan.teile.filter((t) => !t.id && t.primitive?.length).length);
    const fang = pg.fangdatenVon(plan);
    assert.ok(fang.punkte.every((f) => f.p.every(Number.isFinite)));
    if (plan.teile.length) assert.ok(pg.begrenzungVon(plan), a.kennzeichen);
  }
});

test('Echte Daten: alle Körper des Kerns lassen sich bauen, mit endlicher Ausdehnung', { skip: server ? false : 'kein Testserver erreichbar' }, async () => {
  const liste = await holeJson('/api/koerper');
  assert.ok(liste.length > 20);
  let gebaut = 0;
  for (const k of liste) {
    const g = mitDesign('hell', {}, () => k3.baueKoerper(k));
    if (!g) { assert.equal((k.teile ?? []).length === 0 || k.teile.every((t) => !k3.teilGeometrie(t)), true, `Körper ${k.id} (${k.ebene}) nicht gebaut`); continue; }
    gebaut++;
    const b = box(g);
    assert.ok(!b.isEmpty() && [b.min.x, b.max.y, b.min.z].every(Number.isFinite), k.id);
    k3.freigeben(g);
  }
  assert.ok(gebaut > 20);
  // nur die genannten Ids
  const ids = liste.slice(0, 3).map((k) => k.id);
  const teil = await holeJson(k3.koerperUrl(ids));
  assert.deepEqual(teil.map((k) => k.id).sort(), ids.slice().sort());
});

// ---------- Design hell/dunkel ----------
/** Variablen eines CSS-Blocks mit dem genauen Selektor, z. B. ':root' oder ':root[data-design="dunkel"]'. */
function variablen(css, selektor) {
  const i = css.indexOf(selektor + ' {');
  assert.ok(i >= 0, `Block ${selektor} fehlt`);
  const block = css.slice(i, css.indexOf('\n}', i));
  return new Map([...block.matchAll(/(--[a-z0-9-]+)\s*:\s*([^;]+);/g)].map((m) => [m[1], m[2].trim()]));
}

test('Design: dunkler Block nutzt nur Variablennamen des hellen Blocks und deckt alle Farben ab', () => {
  const css = lies('css/hlks.css');
  const hell = variablen(css, ':root'), dunkel = variablen(css, ':root[data-design="dunkel"]');
  for (const name of dunkel.keys()) assert.ok(hell.has(name), `${name} nur im dunklen Design`);
  // Alle Farb-Variablen (Wert ist Hex-Farbe) des hellen Designs müssen im dunklen neu gesetzt sein
  for (const [name, wert] of hell) if (/^#[0-9a-f]{3,8}$/i.test(wert)) assert.ok(dunkel.has(name), `${name} fehlt im dunklen Design`);
});

test('Design: Farben aus dem Revit-Bildschirmfoto im dunklen Design', () => {
  const d = variablen(lies('css/hlks.css'), ':root[data-design="dunkel"]');
  const erwartet = { '--kopf': '#222933', '--reiterzeile': '#222933', '--palettenkopf': '#222933', '--trenner': '#222933',
    '--menueband': '#3b4453', '--gruppentitel': '#3b4453', '--optionsleiste': '#3b4453', '--statusleiste': '#3b4453', '--eingabe': '#3b4453',
    '--palette': '#2e3440', '--browser': '#3e4452', '--suchfeld': '#2e343f', '--steuerleiste': '#313945', '--ansichtsreiter-zeile': '#141618',
    '--akzent': '#2b7bb3', '--text': '#f5f5f5', '--text-leise': '#aab1bb', '--zeichenflaeche': '#222933', '--d3-hintergrund': '#222933' };
  for (const [name, wert] of Object.entries(erwartet)) assert.equal(d.get(name), wert, name);
});

test('Design: Revit-Masse in den Variablen (Kopf 32, Reiter 22, Gruppe 70+18, Option 24, Status 22, Steuerleiste 20, Palette 280)', () => {
  const h = variablen(lies('css/hlks.css'), ':root');
  const soll = { '--hoehe-kopf': '32px', '--hoehe-reiterzeile': '22px', '--hoehe-gruppe': '70px', '--hoehe-gruppentitel': '18px',
    '--hoehe-optionsleiste': '24px', '--hoehe-status': '22px', '--hoehe-ansichtsreiter': '22px', '--hoehe-steuerleiste': '20px',
    '--hoehe-palettenkopf': '18px', '--hoehe-zeile': '20px', '--hoehe-feld': '18px', '--hoehe-suchfeld': '22px', '--breite-seitenleiste': '280px', '--schriftgroesse': '12px' };
  for (const [name, wert] of Object.entries(soll)) assert.equal(h.get(name), wert, name);
});

test('Design: keine festen Farben in oberflaeche.css und ansichten.css, kein CSS zoom, Schrift überall 12 px', () => {
  for (const datei of ['css/oberflaeche.css', 'css/ansichten.css']) {
    const css = lies(datei).replace(/\/\*[\s\S]*?\*\//g, '');
    // Ausnahme: die abgeleiteten Hover-/Meldungsflächen im dunklen Block von oberflaeche.css
    const ohneDunkelblock = css.replace(/:root\[data-design="dunkel"\]\s*\{[^}]*\}/, '');
    assert.deepEqual(ohneDunkelblock.match(/#[0-9a-fA-F]{3,8}\b/g) ?? [], [], `${datei}: feste Farben`);
    assert.doesNotMatch(css, /(^|[;{\s])zoom\s*:/, `${datei}: zoom`);
    for (const m of css.matchAll(/font-size:\s*([\d.]+)px/g)) assert.ok(['12', '11', '9'].includes(m[1]), `${datei}: Schriftgrösse ${m[1]}px`);   // 11 Festbreite, 9 Pfeilzeichen
  }
  assert.doesNotMatch(lies('css/hlks.css').replace(/\/\*[\s\S]*?\*\//g, ''), /(^|[;{\s])zoom\s*:/);
});


test('Design: Umschalten setzt das Attribut, merkt es und meldet es den Ansichten; Vorgabe dunkel', async () => {
  const speicher = new Map(), attribute = new Map(), ereignisse = [];
  globalThis.localStorage = { getItem: (k) => speicher.get(k) ?? null, setItem: (k, v) => speicher.set(k, v) };
  globalThis.document = { documentElement: { getAttribute: (n) => attribute.get(n) ?? null, setAttribute: (n, v) => attribute.set(n, v) } };
  globalThis.CustomEvent = class { constructor(art, o) { this.type = art; this.detail = o?.detail; } };
  globalThis.window = { dispatchEvent: (e) => ereignisse.push(e) };
  try {
    const d = await import('../oberflaeche/design.js');
    assert.equal(d.VORGABE, 'dunkel');
    assert.equal(d.gueltigesDesign('blau'), 'dunkel');
    assert.equal(d.gueltigesDesign(undefined), 'dunkel');
    d.designEinrichten();                                   // nichts gemerkt → dunkel
    assert.equal(d.design(), 'dunkel');
    assert.equal(speicher.has('hlks.design'), false);       // Einrichten merkt nichts
    assert.equal(d.designUmschalten(), 'hell');
    assert.equal(attribute.get('data-design'), 'hell');
    assert.equal(speicher.get('hlks.design'), '"hell"');
    assert.equal(ereignisse.at(-1).type, 'hlks-design');
    assert.equal(ereignisse.at(-1).detail.design, 'hell');
    const anzahl = ereignisse.length;
    d.designSetzen('hell');                                 // unverändert → kein neues Ereignis
    assert.equal(ereignisse.length, anzahl);
    d.designEinrichten();                                   // gemerkt hell → bleibt hell
    assert.equal(d.design(), 'hell');
    assert.equal(d.designUmschalten(), 'dunkel');
    assert.equal(ereignisse.length, anzahl + 1);
  } finally { delete globalThis.localStorage; delete globalThis.document; delete globalThis.CustomEvent; delete globalThis.window; }
});

test('Design: design-start.js setzt vor dem Zeichnen das gemerkte Design, sonst dunkel', () => {
  const quelle = lies('js/oberflaeche/design-start.js');
  const lauf = (gemerkt) => {
    const attribute = new Map();
    new Function('localStorage', 'document', quelle)({ getItem: () => gemerkt }, { documentElement: { setAttribute: (n, v) => attribute.set(n, v) } });
    return attribute.get('data-design');
  };
  assert.equal(lauf(null), 'dunkel');
  assert.equal(lauf('"hell"'), 'hell');
  assert.equal(lauf('"dunkel"'), 'dunkel');
  assert.equal(lauf('kaputt{'), 'dunkel');
  assert.match(lies('index.html'), /<script src="js\/oberflaeche\/design-start\.js"><\/script>\s*<link rel="stylesheet" href="css\/hlks\.css">/);
});

// ---------- Unterlagen im Grundriss: Rasterbild (Primitiv «bild») ----------
/** Wendet die SVG-Matrix [a b c d e f] auf einen Punkt (u, v) des Einheitsbildes an. */
const abbilden = (m, u, v) => [m[0] * u + m[2] * v + m[4], m[1] * u + m[3] * v + m[5]];
const gleich = (p, q, eps = 1e-6) => { nahe(p[0], q[0], eps); nahe(p[1], q[1], eps); };

test('Bild: Matrix bildet das Einheitsbild (links oben = Ursprung, y nach unten) auf die vier Ecken im Modell ab', () => {
  // Breite 4000, Höhe 3000, links unten bei (1000, 2000)
  const ecken = [[1000, 2000], [5000, 2000], [5000, 5000], [1000, 5000]];   // unten links, unten rechts, oben rechts, oben links
  const m = pg.bildMatrix(ecken);
  gleich(abbilden(m, 0, 0), [1000, 5000]);   // Bild links oben → oben links
  gleich(abbilden(m, 1, 0), [5000, 5000]);   // rechts oben → oben rechts
  gleich(abbilden(m, 1, 1), [5000, 2000]);   // rechts unten → unten rechts
  gleich(abbilden(m, 0, 1), [1000, 2000]);   // links unten → unten links
  assert.ok(m[3] < 0, 'd < 0: im y-gespiegelten Gruppenraum steht das Bild damit aufrecht');
  // gedreht um 30° (Ähnlichkeitsabbildung: auch die vierte Ecke passt)
  const w = 30 * Math.PI / 180, dreh = ([x, y]) => [x * Math.cos(w) - y * Math.sin(w) + 100, x * Math.sin(w) + y * Math.cos(w) - 50];
  const g = [[0, 0], [2000, 0], [2000, 1000], [0, 1000]].map(dreh);
  const mg = pg.bildMatrix(g);
  gleich(abbilden(mg, 1, 0), g[2]);
  gleich(abbilden(mg, 0, 1), g[0]);
  gleich(abbilden(mg, 1, 1), g[1]);
  // ungültig / entartet
  assert.equal(pg.bildMatrix(null), null);
  assert.equal(pg.bildMatrix([[0, 0], [1, 0], [1, 1]]), null);
  assert.equal(pg.bildMatrix([[0, 0], [0, 0], [0, 0], [0, 0]]), null);
  assert.equal(pg.bildMatrix([[0, 0], [1, 0], [NaN, 1], [0, 1]]), null);
});

test('Bild: <image> mit Adresse der Unterlage, Matrix und Deckkraft; nicht anklickbar', () => {
  const p = { art: 'bild', quelle: 'UL-1', punkte: [[0, 0], [2000, 0], [2000, 1000], [0, 1000]], stil: { stift: null, deckkraft: 0.5 } };
  const svg = pg.primitivSvg(p, ctx(100));
  assert.match(svg, /^<image /);
  assert.match(svg, /href="\/api\/unterlagen\/UL-1\/bild"/);
  assert.match(svg, /transform="matrix\(2000 0 0 -1000 0 1000\)"/);
  assert.match(svg, /opacity="0\.5"/);
  assert.match(svg, /pointer-events="none"/);
  assert.match(svg, /preserveAspectRatio="none"/);
  // volle Deckkraft: kein opacity-Attribut; Sonderzeichen der Id werden in der Adresse kodiert
  const voll = pg.primitivSvg({ ...p, quelle: 'UL 1/ä', stil: { deckkraft: 1 } }, ctx(100));
  assert.doesNotMatch(voll, /opacity/);
  assert.match(voll, /href="\/api\/unterlagen\/UL%201%2F%C3%A4\/bild"/);
  // ohne Quelle oder mit kaputten Ecken: nichts
  assert.equal(pg.primitivSvg({ ...p, quelle: null }, ctx(100)), '');
  assert.equal(pg.primitivSvg({ ...p, punkte: [[0, 0]] }, ctx(100)), '');
});

test('Bild: das Teil der Unterlage ist nie anklickbar, liegt unter allem; Modell-Teile bleiben anklickbar', () => {
  const bild = { art: 'bild', quelle: 'UL-1', punkte: [[0, 0], [2000, 0], [2000, 1000], [0, 1000]], stil: { deckkraft: 0.5 } };
  const wand = { id: 'w1', ebene: 'wand', primitive: [prim('polygon', { punkte: [[0, 0], [1, 0], [1, 1]] })] };
  const svg = pg.planSvg({ massstab: 100, teile: [wand, { id: 'UL-1', ebene: 'grundlage', primitive: [bild] }] });
  assert.ok(svg.indexOf('<image') < svg.indexOf('data-id="w1"'), 'Bild vor (unter) der Wand');
  assert.doesNotMatch(svg, /class="el e-grundlage"/);
  assert.match(svg, /<g class="e-ohne e-grundlage" data-id="UL-1"><image /);
  assert.doesNotMatch(svg.slice(svg.indexOf('e-grundlage'), svg.indexOf('data-id="w1"')), /class="th/);
  assert.match(svg, /class="el e-wand" data-id="w1"/);
  // eine DWG-Unterlage (Linien) bleibt als Teil mit Id wählbar (Kalibrieren wählt sie so)
  const dwg = { id: 'UL-2', ebene: 'grundlage', primitive: [prim('linie', { punkte: [[0, 0], [100, 0]] })] };
  assert.match(pg.planSvg({ massstab: 100, teile: [dwg] }), /class="el e-grundlage" data-id="UL-2"/);
  // Begrenzung schliesst das Bild ein; Fangen: ein Bild liefert keine Fangpunkte
  assert.deepEqual(pg.begrenzungVon({ teile: [{ ebene: 'grundlage', primitive: [bild] }] }), { minx: 0, miny: 0, maxx: 2000, maxy: 1000 });
  assert.deepEqual(pg.fangdatenVon({ teile: [{ id: 'UL-1', ebene: 'grundlage', primitive: [bild] }] }), { punkte: [], kanten: [] });
});

// ---------- Netzdatei lesen (Hintergrund im 3D) ----------
const netzd = await import('./netzdatei.js');

/** Baut eine kleine Netzdatei wie der Kern (Format: Kopf von netzdatei.js). produkte: { label, gid, ent, kote, uebernommen, punkte[[x,y,z]…], dreiecke[[a,b,c]…] } */
function baueNetz(produkte, { fassung = 1, kennung = 'HLKSNETZ' } = {}) {
  const teile = [];
  const puffer = (n, fn) => { const b = new DataView(new ArrayBuffer(n)); fn(b); teile.push(new Uint8Array(b.buffer)); };
  const text = (t) => new Uint8Array([...t].map((c) => c.charCodeAt(0)));
  const beschr = new TextEncoder().encode('Test ä');
  teile.push(text(kennung));
  puffer(8, (b) => { b.setInt32(0, fassung, true); b.setInt32(4, beschr.length, true); });
  teile.push(beschr);
  puffer(4 + 48, (b) => { b.setInt32(0, produkte.length, true); [-1000, -2000, -3000, 9000, 8000, 7000].forEach((v, i) => b.setFloat64(4 + 8 * i, v, true)); });
  for (const p of produkte) {
    puffer(4, (b) => b.setInt32(0, p.label, true));
    teile.push(text(p.gid.padEnd(22, 'x').slice(0, 22)));
    teile.push(new Uint8Array([p.ent.length]), text(p.ent));
    puffer(8 + 1 + 24 + 8, (b) => {
      b.setFloat64(0, p.kote ?? NaN, true);
      b.setUint8(8, p.uebernommen ? 1 : 0);
      for (let i = 0; i < 6; i++) b.setFloat32(9 + 4 * i, i, true);
      b.setInt32(33, p.punkte.length, true); b.setInt32(37, p.dreiecke.length, true);
    });
    puffer(12 * p.punkte.length, (b) => p.punkte.forEach((q, i) => q.forEach((v, j) => b.setFloat32(12 * i + 4 * j, v, true))));
    puffer(12 * p.dreiecke.length, (b) => p.dreiecke.forEach((q, i) => q.forEach((v, j) => b.setInt32(12 * i + 4 * j, v, true))));
  }
  const gesamt = new Uint8Array(teile.reduce((s, t) => s + t.length, 0));
  let o = 0; for (const t of teile) { gesamt.set(t, o); o += t.length; }
  return gesamt.buffer;
}

const PRODUKTE = [
  { label: 11, gid: 'GID-WAND-HINTERGRUND', ent: 'IfcWall', kote: 0, uebernommen: false, punkte: [[0, 0, 0], [1000, 0, 0], [1000, 2000, 3000]], dreiecke: [[0, 1, 2]] },
  { label: 12, gid: 'GID-UEBERNOMMEN', ent: 'IfcSlab', kote: 2900, uebernommen: true, punkte: [[0, 0, 0], [5000, 0, 0], [5000, 5000, 0], [0, 5000, 0]], dreiecke: [[0, 1, 2], [0, 2, 3]] },
  { label: 13, gid: 'GID-TREPPE', ent: 'IfcStair', kote: null, uebernommen: false, punkte: [[2000, 3000, 500], [3000, 3000, 500], [3000, 4000, 1500], [2000, 4000, 1500]], dreiecke: [[0, 1, 2], [0, 2, 3], [3, 2, 1]] },
];

test('Netzdatei: Kopf, Produkte und nur die Hintergrund-Produkte zusammengeführt (three-Meter, Indizes um den Versatz verschoben)', () => {
  const n = netzd.leseNetz(baueNetz(PRODUKTE));
  assert.equal(n.fassung, 1);
  assert.equal(n.beschreibung, 'Test ä');
  assert.deepEqual(n.begrenzung, [-1000, -2000, -3000, 9000, 8000, 7000]);
  assert.equal(n.produkte.length, 3);
  assert.deepEqual(n.produkte.map((p) => [p.label, p.entitaet, p.kote, p.uebernommen, p.punkte, p.dreiecke]),
    [[11, 'IfcWall', 0, false, 3, 1], [12, 'IfcSlab', 2900, true, 4, 2], [13, 'IfcStair', null, false, 4, 3]]);
  assert.equal(n.produkte[0].globalId.length, 22);
  // Hintergrund = Wand (3 Punkte, 1 Dreieck) + Treppe (4 Punkte, 3 Dreiecke); die übernommene Decke fehlt
  assert.equal(n.punkte, 7);
  assert.equal(n.dreiecke, 4);
  assert.ok(n.positionen instanceof Float32Array && n.indizes instanceof Uint32Array);
  assert.equal(n.positionen.length, 21);
  assert.equal(n.indizes.length, 12);
  // Punkt 3 der Wand: Modell (1000, 2000, 3000) mm → three (x = 1, y = 3, z = −2) m
  assert.deepEqual([...n.positionen.slice(6, 9)], [1, 3, -2]);
  // erster Punkt der Treppe (Versatz 3): Modell (2000, 3000, 500) → (2, 0.5, −3)
  assert.deepEqual([...n.positionen.slice(9, 12)], [2, 0.5, -3]);
  assert.deepEqual([...n.indizes], [0, 1, 2, 3, 4, 5, 3, 5, 6, 6, 5, 4]);
});

test('Netzdatei: Auswahl übernommen und alle; leeres Ergebnis ist kein Fehler', () => {
  const daten = baueNetz(PRODUKTE);
  const u = netzd.leseNetz(daten, { nur: 'uebernommen' });
  assert.equal(u.dreiecke, 2);
  assert.equal(u.punkte, 4);
  assert.equal(netzd.leseNetz(daten, { nur: 'alle' }).dreiecke, 6);
  assert.throws(() => netzd.leseNetz(daten, { nur: 'bogen' }), /Auswahl/);
  const leer = netzd.leseNetz(baueNetz([]));
  assert.equal(leer.produkte.length, 0);
  assert.equal(leer.positionen.length, 0);
  // nur übernommene Produkte → Hintergrund leer
  assert.equal(netzd.leseNetz(baueNetz([PRODUKTE[1]])).dreiecke, 0);
  assert.equal(k3.netzGeometrie(netzd.leseNetz(baueNetz([PRODUKTE[1]]))), null);
});

test('Netzdatei: Fehler sind deutsch und verständlich (falsche Kennung, Fassung, abgeschnitten, ungültiger Index)', () => {
  assert.throws(() => netzd.leseNetz(baueNetz(PRODUKTE, { kennung: 'NETZHLKS' })), /keine HLKS-Netzdatei/);
  assert.throws(() => netzd.leseNetz(baueNetz(PRODUKTE, { fassung: 7 })), /Fassung 7/);
  assert.throws(() => netzd.leseNetz(new ArrayBuffer(3)), /abgeschnitten/);
  const ganz = baueNetz(PRODUKTE);
  for (const kuerzer of [10, 70, 150, ganz.byteLength - 1]) assert.throws(() => netzd.leseNetz(ganz.slice(0, kuerzer)), /abgeschnitten/, `bei ${kuerzer} Byte`);
  // Index ausserhalb der Punkte des Produkts: entartetes Dreieck auf den ersten Punkt des Produkts, nie ein Fremdpunkt
  const schlecht = netzd.leseNetz(baueNetz([PRODUKTE[0], { ...PRODUKTE[2], dreiecke: [[0, 1, 99]] }]));
  assert.deepEqual([...schlecht.indizes], [0, 1, 2, 3, 4, 3]);
  // aus einer Teilansicht (Byteversatz ≠ 0) lesen
  const mit = new Uint8Array(ganz.byteLength + 5); mit.set(new Uint8Array(ganz), 5);
  assert.equal(netzd.leseNetz(mit.subarray(5)).dreiecke, 4);
});

test('Netzdatei: Kennung und Fassung stimmen mit dem Schreiber im Kern überein', () => {
  const cs = kernQuelle('Import/IfcNetz.cs');
  if (!cs) return;
  assert.match(cs, new RegExp(`Kennung = "${netzd.KENNUNG}"`));
  assert.match(cs, /Fassung = 3;/);   // Fassung 3 seit Nachtrag B3c (Material), 2 seit Modul 3b (B1b); 1 und 2 bleiben lesbar (netzdatei.test.mjs)
  assert.match(cs, /22 Byte GlobalId/);
});

test('Netzdatei: grosses Netz (1.5 Mio. Dreiecke) wird in wenigen Sekunden zu einer Geometrie', () => {
  // 100 Produkte à 17 000 Dreiecke, davon 10 übernommen
  const proStueck = 17000, punkteProStueck = 8500;
  const punkte = Array.from({ length: punkteProStueck }, (_, i) => [i % 100 * 10, Math.floor(i / 100) * 10, i % 7]);
  const dreiecke = Array.from({ length: proStueck }, (_, i) => [i % punkteProStueck, (i + 1) % punkteProStueck, (i + 100) % punkteProStueck]);
  const grosse = Array.from({ length: 100 }, (_, i) => ({ label: i, gid: 'G' + i, ent: 'IfcWall', kote: 0, uebernommen: i % 10 === 0, punkte, dreiecke }));
  const daten = baueNetz(grosse);
  const t0 = performance.now();
  const n = netzd.leseNetz(daten);
  const ms = performance.now() - t0;
  assert.equal(n.dreiecke, 90 * proStueck);
  assert.equal(n.punkte, 90 * punkteProStueck);
  assert.ok(ms < 5000, `Lesen dauerte ${Math.round(ms)} ms`);
  const geo = k3.netzGeometrie(n);
  assert.equal(geo.index.count, 90 * proStueck * 3);
  assert.equal(geo.attributes.position.count, 90 * punkteProStueck);
  assert.equal(geo.userData.geteilt, true);
  console.log(`# Netz mit ${n.dreiecke} Dreiecken (${Math.round(daten.byteLength / 1e6)} MB) gelesen in ${Math.round(ms)} ms`);
});

// ---------- Netz als three-Objekt ----------
test('3D: Körperteil «netz» wird zum Halter; Mesh durchscheinend, doppelseitig, nie ein Treffer; geteilte Geometrie bleibt beim Freigeben', () => {
  const k = { id: 'UL-9', ebene: 'sonst', teile: [{ art: 'netz', quelle: 'UL-9', farbe: '#8a8a8a', deckkraft: 0.4, material: 'sonst' }] };
  const g = k3.baueKoerper(k);
  assert.ok(g, 'ein Körper nur aus einem Netz ist trotzdem ein Körper');
  const halter = g.children[0];
  assert.deepEqual(halter.userData.netz, { quelle: 'UL-9', farbe: '#8a8a8a', deckkraft: 0.4 });
  assert.equal(g.userData.materialien.length, 0, 'nie hervorgehoben');
  // ohne Quelle: kein Halter; Standard-Deckkraft 0.5
  assert.equal(k3.baueKoerper({ id: 'x', teile: [{ art: 'netz' }] }), null);
  assert.equal(k3.baueKoerper({ id: 'x', teile: [{ art: 'netz', quelle: 'U' }] }).children[0].userData.netz.deckkraft, 0.5);

  const geo = k3.netzGeometrie(netzd.leseNetz(baueNetz(PRODUKTE)));
  const mesh = k3.netzMesh(geo, '#8a8a8a', 0.4);
  assert.equal(mesh.material.side, THREE.DoubleSide);
  assert.equal(mesh.material.transparent, true);
  assert.equal(mesh.material.opacity, 0.4);
  assert.equal(mesh.material.depthWrite, false);
  // Raycast trifft nie; die Geometrie selbst würde treffen (Wand-Dreieck 0,0 – 1,0 – 1,2 in der Grundfläche)
  halter.add(mesh);
  const von = new THREE.Vector3(0.9, 5, -0.2), unten = new THREE.Vector3(0, -1, 0);
  assert.equal(new THREE.Raycaster(von, unten).intersectObject(g, true).length, 0);
  const gegenprobe = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ side: THREE.DoubleSide }));
  assert.ok(new THREE.Raycaster(von, unten).intersectObject(gegenprobe).length > 0, 'Gegenprobe: ohne Sperre würde es treffen');
  // Freigeben lässt die geteilte Geometrie stehen (sie gehört dem Zwischenspeicher der Ansicht), das Material nicht
  let geoFrei = 0, matFrei = 0;
  geo.addEventListener('dispose', () => geoFrei++);
  mesh.material.addEventListener('dispose', () => matFrei++);
  k3.freigeben(g);
  assert.equal(geoFrei, 0);
  assert.equal(matFrei, 1);
  // undurchsichtig, wenn Deckkraft 1
  const m1 = k3.netzMesh(geo, '#8a8a8a', 1);
  assert.equal(m1.material.transparent, false);
  assert.equal(m1.material.depthWrite, true);
});

test('3D: Schlüssel und Adresse der Netzdatei – neu geladen wird nur bei geändertem Dateinamen', () => {
  assert.equal(k3.netzSchluessel({ id: 'u1', werte: { netz: 'a_x.ifc.netz-1.bin' } }), 'a_x.ifc.netz-1.bin');
  assert.equal(k3.netzSchluessel({ id: 'u1', werte: {} }), 'u1');
  assert.equal(k3.netzSchluessel(null), '');
  assert.equal(k3.netzUrl('UL 1', 'a b.bin'), '/api/unterlagen/UL%201/netz?v=a%20b.bin');
  assert.equal(k3.netzUrl('UL-1'), '/api/unterlagen/UL-1/netz');
  // ansicht3d.js benutzt den Schlüssel für den Zwischenspeicher und lädt über diese Adresse
  const q = lies('js/ansichten/ansicht3d.js');
  assert.match(q, /alt\.schluessel === schluessel/);
  assert.match(q, /netzUrl\(quelle, schluessel\)/);
});

test('Deckkraft: ohne Füllung gilt sie der Linie (stroke-opacity), mit Füllung nur der Füllung', () => {
  const linie = pg.primitivSvg(prim('linie', { punkte: [[0, 0], [1000, 0]] }, { deckkraft: 0.5 }), ctx(50));
  assert.match(linie, /stroke-opacity="0\.5"/);
  assert.doesNotMatch(linie, /fill-opacity/);
  // Polygon ohne Füllung, Kreis, Bogen: ebenfalls Linie
  assert.match(pg.primitivSvg(prim('polygon', { punkte: [[0, 0], [1, 0], [1, 1]] }, { deckkraft: 0.3 }), ctx(50)), /stroke-opacity="0\.3"/);
  assert.match(pg.primitivSvg(prim('kreis', { punkte: [[0, 0]], radius: 100 }, { deckkraft: 0.5 }), ctx(50)), /stroke-opacity="0\.5"/);
  assert.match(pg.primitivSvg(prim('bogen', { punkte: [[0, 0]], radius: 100, von: 0, bis: 90 }, { deckkraft: 0.5 }), ctx(50)), /stroke-opacity="0\.5"/);
  // mit Füllung (HLKS-Kanal, Deckkraft auf der Füllung): Linie bleibt voll deckend
  const kanal = pg.primitivSvg(prim('polygon', { punkte: [[0, 0], [1, 0], [1, 1]] }, { fuellung: '#336699', deckkraft: 0.2 }), ctx(50));
  assert.match(kanal, /fill-opacity="0\.2"/);
  assert.doesNotMatch(kanal, /stroke-opacity/);
  // volle oder fehlende Deckkraft: nichts
  assert.doesNotMatch(pg.primitivSvg(prim('linie', { punkte: [[0, 0], [1000, 0]] }, { deckkraft: 1 }), ctx(50)), /opacity/);
  assert.doesNotMatch(pg.primitivSvg(prim('linie', { punkte: [[0, 0], [1000, 0]] }), ctx(50)), /opacity/);
});
