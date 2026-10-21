// Tests zu N12 (IFC: Original oder Nachbau als Unterlage zeigen, je eigene Farbe) ohne Browser: ifc-darstellung.js, filter.js, koerper3d.js
// (Übersteuerung des Nachbaus) und die Auswahl der Produkte des Netzes aus der Ansicht (netzdatei.js, Option nur).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/ifc-darstellung.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { register } from 'node:module';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const threeUrl = pathToFileURL(path.resolve(hier, '../../lib/three/three.module.js')).href;
register('data:text/javascript,' + encodeURIComponent(
  `export async function resolve(s, c, n) { if (s === 'three') return { url: ${JSON.stringify(threeUrl)}, shortCircuit: true }; return n(s, c); }`));

const d = await import('./ifc-darstellung.js');
const { sichtbar } = await import('./filter.js');
const k3 = await import('./koerper3d.js');
const { leseNetz } = await import('./netzdatei.js');

const ul = (werte = {}) => ({ id: 'UL1', klasse: 'Unterlage', werte: { art: 'ifc', ...werte } });
const wand = (extra = {}) => ({ id: 'W1', klasse: 'Wand', schicht: 'bauteil', geometrie: { art: 'linie' }, kennungen: { ifc: 'abc', quelle: 'UL1' }, ...extra });
const eigene = () => ({ id: 'W2', klasse: 'Wand', schicht: 'bauteil', geometrie: { art: 'linie' } });
const modell = (u) => (id) => (id === 'UL1' ? u : undefined);
const ansicht = (werte = {}) => ({ id: 'AN1', werte: { art: '3d', ...werte } });

test('N12 Modus: Vorgabe nachbau; unbekannte Werte und andere Darstellungen gelten als nachbau', () => {
  assert.equal(d.ifcModus(ansicht()), 'nachbau');
  assert.equal(d.ifcModus(ansicht({ ifcDarstellung: 'original' })), 'original');
  assert.equal(d.ifcModus(ansicht({ ifcDarstellung: 'beide' })), 'beide');
  assert.equal(d.ifcModus(ansicht({ ifcDarstellung: 'quatsch' })), 'nachbau');
  assert.equal(d.ifcModus(ansicht({ ifcDarstellung: 'original', darstellung: 'huelle' })), 'nachbau');
  assert.equal(d.ifcModus(ansicht({ ifcDarstellung: 'original', darstellung: 'standard' })), 'original');
  assert.equal(d.ifcModus(null), 'nachbau');
});

test('N12 3D: die Auswahl der Produkte kommt aus der Ansicht (nur = hintergrund | alle)', () => {
  assert.equal(d.netzAuswahl(ansicht()), 'hintergrund');
  assert.equal(d.netzAuswahl(ansicht({ ifcDarstellung: 'original' })), 'alle');
  assert.equal(d.netzAuswahl(ansicht({ ifcDarstellung: 'beide' })), 'alle');
  assert.equal(d.zeigtOriginal(ansicht({ ifcDarstellung: 'beide' })), true);
  assert.equal(d.zeigtOriginal(ansicht()), false);
});

// Netzdatei mit zwei Produkten: Produkt 1 übernommen (1), Produkt 2 Hintergrund (0); je ein Dreieck. Aufbau wie netzdatei.js (Fassung 1).
function netzDatei() {
  const teile = [];
  const u8 = (n) => teile.push(Uint8Array.of(n));
  const i32 = (n) => { const b = new ArrayBuffer(4); new DataView(b).setInt32(0, n, true); teile.push(new Uint8Array(b)); };
  const f64 = (n) => { const b = new ArrayBuffer(8); new DataView(b).setFloat64(0, n, true); teile.push(new Uint8Array(b)); };
  const f32 = (n) => { const b = new ArrayBuffer(4); new DataView(b).setFloat32(0, n, true); teile.push(new Uint8Array(b)); };
  const ascii = (t) => teile.push(new TextEncoder().encode(t));
  ascii('HLKSNETZ'); i32(1); i32(0); i32(2);
  for (let i = 0; i < 6; i++) f64(i < 3 ? 0 : 10);
  for (const [label, gid, ueb] of [[1, 'A'.repeat(22), 1], [2, 'B'.repeat(22), 0]]) {
    i32(label); ascii(gid); const e = new TextEncoder().encode('IfcWall'); u8(e.length); teile.push(e); f64(Number.NaN); u8(ueb);
    for (let i = 0; i < 6; i++) f32(i < 3 ? 0 : 1);
    i32(3); i32(1);
    for (const p of [[0, 0, 0], [1, 0, 0], [0, 1, 0]]) for (const c of p) f32(c);
    i32(0); i32(1); i32(2);
  }
  const n = teile.reduce((s, t) => s + t.length, 0), aus = new Uint8Array(n);
  let o = 0; for (const t of teile) { aus.set(t, o); o += t.length; }
  return aus;
}

test('N12 3D: leseNetz mit nur aus der Ansicht – nachbau liest den Hintergrund, original und beide alle Produkte', () => {
  const datei = netzDatei();
  assert.equal(leseNetz(datei, { nur: d.netzAuswahl(ansicht()) }).dreiecke, 1);
  assert.equal(leseNetz(datei, { nur: d.netzAuswahl(ansicht({ ifcDarstellung: 'original' })) }).dreiecke, 2);
  assert.equal(leseNetz(datei, { nur: d.netzAuswahl(ansicht({ ifcDarstellung: 'beide' })) }).dreiecke, 2);
});

test('N12 Filter: im Modus original entfällt der Nachbau der IFC-Unterlage, eigene Elemente bleiben', () => {
  const u = ul();
  assert.equal(sichtbar(wand(), ansicht(), modell(u)), true);
  assert.equal(sichtbar(wand(), ansicht({ ifcDarstellung: 'beide' }), modell(u)), true);
  assert.equal(sichtbar(wand(), ansicht({ ifcDarstellung: 'original' }), modell(u)), false);
  assert.equal(sichtbar(eigene(), ansicht({ ifcDarstellung: 'original' }), modell(u)), true);
  // ohne nachId (Listen, ältere Aufrufer) wie bisher
  assert.equal(sichtbar(wand(), ansicht({ ifcDarstellung: 'original' })), true);
});

test('N12 Filter: ausgeblendete Unterlage, andere Dateiart und andere Darstellung lassen den Nachbau stehen', () => {
  const a = ansicht({ ifcDarstellung: 'original' });
  assert.equal(sichtbar(wand(), a, modell(ul({ anzeigen: false }))), true);
  assert.equal(sichtbar(wand(), a, modell({ id: 'UL1', klasse: 'Unterlage', werte: { art: 'dwg' } })), true);
  assert.equal(sichtbar(wand(), ansicht({ ifcDarstellung: 'original', darstellung: 'huelle' }), modell(ul())), true);
  assert.equal(sichtbar(wand({ kennungen: { quelle: 'UL9' } }), a, modell(ul())), true);
});

test('N12 Nachbau-Stil: Farbe und Deckkraft aus der Unterlage; ohne Angabe keine Übersteuerung', () => {
  const a = ansicht();
  assert.equal(d.nachbauStil(wand(), a, modell(ul())), null);
  assert.deepEqual(d.nachbauStil(wand(), a, modell(ul({ farbeNachbau: '#FF8000', deckkraftNachbau: 0.4 }))), { farbe: '#ff8000', deckkraft: 0.4 });
  assert.deepEqual(d.nachbauStil(wand(), a, modell(ul({ deckkraftNachbau: 0.25 }))), { farbe: null, deckkraft: 0.25 });
  assert.deepEqual(d.nachbauStil(wand(), a, modell(ul({ farbeNachbau: 'rot' }))), null);
  assert.equal(d.nachbauStil(eigene(), a, modell(ul({ farbeNachbau: '#ff8000' }))), null);
  assert.equal(d.nachbauStil(wand(), ansicht({ darstellung: 'aussparungen' }), modell(ul({ farbeNachbau: '#ff8000' }))), null);
});

test('N12 Original-Stil: Vorgabe grau 0.5, sonst die Werte der Unterlage', () => {
  assert.deepEqual(d.originalStil(ul()), { farbe: '#8a8a8a', deckkraft: 0.5 });
  assert.deepEqual(d.originalStil(ul({ farbeOriginal: '#0066FF', deckkraft: 0.3 })), { farbe: '#0066ff', deckkraft: 0.3 });
  assert.deepEqual(d.originalStil(ul({ farbeOriginal: 'blau', deckkraft: 7 })), { farbe: '#8a8a8a', deckkraft: 1 });
});

test('N12 3D: baueKoerper übersteuert Farbe und Deckkraft des Nachbaus, ohne Angabe bleibt die normale Darstellung', () => {
  const k = { id: 'W1', ebene: 'wand', teile: [{ art: 'quader', mitte: [0, 0, 1000], masse: [1000, 200, 2000], material: 'wand' }] };
  const normal = k3.baueKoerper(k);
  const o = k3.baueKoerper(k, { ueberschreiben: { farbe: '#ff8000', deckkraft: 0.4 } });
  const mat = (g) => g.userData.materialien[0];
  assert.equal(mat(o).color.getHexString(), 'ff8000');
  assert.equal(mat(o).opacity, 0.4);
  assert.equal(mat(o).transparent, true);
  assert.notEqual(mat(normal).color.getHexString(), 'ff8000');
  // nur die Deckkraft: Farbe bleibt die des Materials
  const nurDeck = k3.baueKoerper(k, { ueberschreiben: { farbe: null, deckkraft: 0.25 } });
  assert.equal(mat(nurDeck).color.getHexString(), mat(normal).color.getHexString());
  assert.equal(mat(nurDeck).opacity, 0.25);
});

test('N12 3D: der Halter des Netzes trägt Farbe und Deckkraft des Originals aus dem Körper des Kerns', () => {
  const k = { id: 'UL1', ebene: 'grundlage', teile: [{ art: 'netz', quelle: 'UL1', material: 'sonst', farbe: '#0066ff', deckkraft: 0.35 }] };
  const o = k3.baueKoerper(k);
  const halter = [];
  o.traverse((h) => { if (h.userData.netz && h.userData.netz !== true) halter.push(h); });
  assert.equal(halter.length, 1);
  assert.equal(halter[0].userData.netz.quelle, 'UL1');
  assert.equal(String(halter[0].userData.netz.farbe).toLowerCase(), '#0066ff');
  assert.equal(halter[0].userData.netz.deckkraft, 0.35);
});
