// Tests der Darstellung «Hülle» (Modul 1c, Auftrag C1b) ohne Browser: Farben (Spiegel des Kerns), Legende, Infotext, 3D-Flächen aus den
// Raumgrenzen (huelle3d.js mit three), Treffer, blassere Architekturkörper, Plangrafik-Ebene huelle.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/huelle-darstellung.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { register } from 'node:module';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';
import { readFileSync, existsSync } from 'node:fs';

const hier = path.dirname(fileURLToPath(import.meta.url));
const threeUrl = pathToFileURL(path.resolve(hier, '../../lib/three/three.module.js')).href;
register('data:text/javascript,' + encodeURIComponent(
  `export async function resolve(s, c, n) { if (s === 'three') return { url: ${JSON.stringify(threeUrl)}, shortCircuit: true }; return n(s, c); }`));

const d = await import('./huelle-darstellung.js');
const h3 = await import('./huelle3d.js');
const pg = await import('./plangrafik.js');
const k3 = await import('./koerper3d.js');
const THREE = await import(threeUrl);

const kernOrdner = path.resolve(hier, '../../../../Hlks.Kern');
const kernQuelle = (rel) => { const p = path.join(kernOrdner, rel); return existsSync(p) ? readFileSync(p, 'utf8') : null; };

// ---------- Farben und Ebene ----------
test('Farben: Spiegel von HuelleGrafik.cs (Annahme Farbe)', () => {
  const cs = kernQuelle('Grafik/HuelleGrafik.cs');
  if (cs) {
    const farbe = (name) => cs.match(new RegExp(`${name} = "(#[0-9a-fA-F]{6})"`))[1];
    assert.equal(d.HUELLE_FARBEN.aussen, farbe('FarbeAussen'));
    assert.equal(d.HUELLE_FARBEN.erdreich, farbe('FarbeErdreich'));
    assert.equal(d.HUELLE_FARBEN.unbeheizt, farbe('FarbeUnbeheizt'));
    assert.equal(d.HUELLE_FARBEN.beheizt, farbe('FarbeBeheizt'));
  }
  assert.deepEqual([...d.ANGRENZUNGEN], ['aussen', 'erdreich', 'unbeheizt', 'beheizt']);
  const cr = kernQuelle('Huelle/Raumgrenzen.cs');
  if (cr) assert.match(cr, /Angrenzungen = \["aussen", "erdreich", "unbeheizt", "beheizt"\]/);
  assert.equal(new Set(Object.values(d.HUELLE_FARBEN)).size, 4, 'vier verschiedene Farben');
});

test('Ebene huelle liegt über den Räumen, unter den Wänden (Spiegel des Kerns)', () => {
  const i = (e) => pg.EBENEN.indexOf(e);
  assert.ok(i('raum') < i('huelle') && i('huelle') < i('wand'), pg.EBENEN.join());
  assert.ok(pg.ebenenRang('huelle') > pg.ebenenRang('raum') && pg.ebenenRang('huelle') < pg.ebenenRang('wand'));
  const teile = [{ id: 'w', ebene: 'wand' }, { id: 'h', ebene: 'huelle' }, { id: 'r', ebene: 'raum' }];
  assert.deepEqual(pg.nachEbenen(teile).map((t) => t.id), ['r', 'h', 'w']);
});

test('Plangrafik: Hüllstreifen werden im dunklen Design nicht gedämpft, Raumfüllungen schon', () => {
  const stil = { stift: null, fuellung: '#e0482f', nurBildschirm: true };
  const poly = { art: 'polygon', punkte: [[0, 0], [1000, 0], [1000, 100], [0, 100]], stil };
  const svg = pg.planSvg({ massstab: 50, teile: [
    { id: null, ebene: 'huelle', primitive: [poly] },
    { id: 'r', ebene: 'raum', primitive: [poly] },
  ] });
  const huelle = svg.match(/<g class="e-ohne e-huelle">(.*?)<\/g>/)[1];
  assert.doesNotMatch(huelle, /class="[^"]*\bbf\b/);
  assert.match(huelle, /fill="#e0482f"/);
  assert.match(svg.match(/<g class="el e-raum"[^>]*>(.*?)<\/g>/)[1], /class="bf"/);
});

test('Plangrafik: Polygon mit Loch (Raum mit Insel) wird mit zwei Ringen gezeichnet, ohne Steg', () => {
  const p = { art: 'polygon', punkte: [[0, 0], [10000, 0], [10000, 8000], [0, 8000]], loecher: [[[4000, 3000], [4000, 5000], [6000, 5000], [6000, 3000]]], stil: { stift: 'duenn' } };
  const dpfad = pg.polygonPfad(p);
  assert.equal((dpfad.match(/M/g) ?? []).length, 2);
  assert.equal((dpfad.match(/Z/g) ?? []).length, 2);
  assert.equal(dpfad, 'M0 0L10000 0L10000 8000L0 8000ZM4000 3000L4000 5000L6000 5000L6000 3000Z');
  assert.match(pg.primitivSvg(p, { massstab: 50, schraffuren: new Map(), muster: new Map() }), /fill-rule="evenodd"/);
});

// ---------- Legende und Infotext ----------
const antwort = (je) => ({ summen: { jeAngrenzung: je } });

test('Legende: vier Angrenzungen in fester Reihenfolge, fehlende mit 0, Flächen aus der Abfrage', () => {
  const daten = d.legendeDaten(antwort({ beheizt: { flaeche_m2: 410.25 }, aussen: { flaeche_m2: 12345.678, ohneU_m2: 3 } }));
  assert.deepEqual(daten.map((x) => x.angrenzung), ['aussen', 'erdreich', 'unbeheizt', 'beheizt']);
  assert.deepEqual(daten.map((x) => x.flaeche), [12345.678, 0, 0, 410.25]);
  assert.deepEqual(daten.map((x) => x.farbe), [d.HUELLE_FARBEN.aussen, d.HUELLE_FARBEN.erdreich, d.HUELLE_FARBEN.unbeheizt, d.HUELLE_FARBEN.beheizt]);
  const html = d.legendeHtml(daten, { titel: 'Hülle EG' });
  assert.match(html, /Hülle EG/);
  assert.match(html, /12’345\.7 m²/);
  assert.match(html, /410\.3 m²/);
  assert.equal((html.match(/class="hl-zeile"/g) ?? []).length, 4);
  for (const f of Object.values(d.HUELLE_FARBEN)) assert.ok(html.includes(`background:${f}`), f);
  assert.deepEqual(d.legendeDaten(null).map((x) => x.flaeche), [0, 0, 0, 0]);
});

test('Legende: Fehler als Text, Titel wird maskiert', () => {
  const html = d.legendeHtml([], { titel: '<b>x</b>', fehler: 'kaputt <i>' });
  assert.match(html, /&lt;b&gt;x/);
  assert.match(html, /kaputt &lt;i&gt;/);
  assert.doesNotMatch(html, /hl-zeile/);
});

test('formatM2: Hochkomma als Tausendertrenner, eine Nachkommastelle', () => {
  assert.equal(d.formatM2(0), '0.0');
  assert.equal(d.formatM2(999.96), '1’000.0');
  assert.equal(d.formatM2(1234567.84), '1’234’567.8');
  assert.equal(d.formatM2(-12.34), '−12.3');
  assert.equal(d.formatM2(NaN), '–');
});

test('istHuelle: nur Grundriss und 3D mit darstellung = huelle', () => {
  assert.equal(d.istHuelle({ werte: { art: 'grundriss', darstellung: 'huelle' } }), true);
  assert.equal(d.istHuelle({ werte: { art: '3d', darstellung: 'huelle' } }), true);
  assert.equal(d.istHuelle({ werte: { art: '3d', darstellung: 'standard' } }), false);
  assert.equal(d.istHuelle({ werte: { art: '3d' } }), false);
  assert.equal(d.istHuelle({ werte: { art: 'liste', darstellung: 'huelle' } }), false);
  assert.equal(d.istHuelle(null), false);
});

test('Infofeld: Raum, Nachbar, Bauteil, Fläche und U (wenn vorhanden)', () => {
  const z = { raum: 'R-1', nachbar: 'aussen', angrenzung: 'aussen', art: 'wand', bauteil: 'W-1', aufbau: 'AUF-1', flaeche_m2: 12.3456, u: 0.25 };
  const f = d.infoZeilen(z);
  assert.deepEqual(f.map((x) => x.name), ['Raum', 'Nachbar', 'Wand', 'Angrenzung', 'Fläche', 'Aufbau', 'U']);
  assert.equal(f.find((x) => x.name === 'Fläche').wert, '12.35 m²');
  assert.equal(f.find((x) => x.name === 'U').wert, '0.25 W/(m²·K)');
  assert.equal(d.infoZeilen({ ...z, u: null, aufbau: null }).some((x) => x.name === 'U'), false);
  assert.equal(d.infoZeilen({ ...z, bauteil: null, offen: true, art: 'wand' })[2].wert, 'offen (kein Bauteil)');
  assert.match(d.infoHtml(z), /W-1/);
  assert.equal(d.infoHtml(null), '');
  assert.match(d.infoHtml({ ...z, raum: '<x>' }), /&lt;x&gt;/);
});

// ---------- 3D-Flächen ----------
const seite = (extra = {}) => ({ raum: 'R', nachbar: 'aussen', angrenzung: 'aussen', art: 'wand', bauteil: 'W-1', flaeche_m2: 15.6, punkte: [[0, 0], [6000, 0]], unten: 0, oben: 2600, ...extra });

/** Fläche (m²) aller Dreiecke einer Geometrie in der waagrechten Ebene (x, z) bzw. gesamt im Raum. */
function dreiecksFlaeche(geom, ebene = 'xz') {
  const p = geom.getAttribute('position');
  let s = 0;
  for (let i = 0; i < p.count; i += 3) {
    const a = new THREE.Vector3().fromBufferAttribute(p, i), b = new THREE.Vector3().fromBufferAttribute(p, i + 1), c = new THREE.Vector3().fromBufferAttribute(p, i + 2);
    if (ebene === 'xz') s += Math.abs((b.x - a.x) * (c.z - a.z) - (c.x - a.x) * (b.z - a.z)) / 2;
    else s += new THREE.Vector3().subVectors(b, a).cross(new THREE.Vector3().subVectors(c, a)).length() / 2;
  }
  return s;
}

test('3D Seite: senkrechtes Viereck Von/Bis × unten/oben, 12 mm in den Raum (links der Laufrichtung) versetzt', () => {
  const e = h3.seitenEcken(seite());
  assert.deepEqual(e, [[0, 12, 0], [6000, 12, 0], [6000, 12, 2600], [0, 12, 2600]]);
  const g = h3.huelleGeometrien([seite()]);
  const a = g.get('aussen');
  assert.equal(a.dreiecke, 2);
  assert.deepEqual([...a.zeile], [0, 0]);
  const box = new THREE.Box3().setFromBufferAttribute(a.geometrie.getAttribute('position'));
  // three: x = x, y = z (Höhe), z = −y
  assert.deepEqual([box.min.x, box.min.y, box.min.z, box.max.x, box.max.y, box.max.z].map((v) => Math.round(v * 1e6) / 1e6 + 0), [0, 0, -0.012, 6, 2.6, -0.012]);
  assert.ok(Math.abs(dreiecksFlaeche(a.geometrie, 'voll') - 6 * 2.6) < 1e-4);
  // Nordkante (Laufrichtung nach −x): Raum liegt links = nach Süden
  const nord = h3.seitenEcken(seite({ punkte: [[6000, 4000], [0, 4000]] }));
  assert.equal(nord[0][1], 4000 - 12);
});

test('3D Fenster und Türen liegen doppelt so weit vor der Wand; ungültige Zeilen werden übergangen', () => {
  assert.equal(h3.seitenEcken(seite({ art: 'fenster' }))[0][1], 24);
  assert.equal(h3.seitenEcken(seite({ art: 'tuer' }))[0][1], 24);
  assert.equal(h3.seitenEcken(seite({ punkte: [[0, 0], [0.2, 0]] })), null);
  assert.equal(h3.seitenEcken(seite({ unten: 100, oben: 100 })), null);
  assert.equal(h3.seitenEcken(seite({ punkte: null })), null);
  const g = h3.huelleGeometrien([seite({ unten: NaN }), seite({ angrenzung: 'gibtsnicht' }), null, {}]);
  assert.ok([...g.values()].every((x) => x.geometrie === null && x.dreiecke === 0));
});

/** Ring 10 × 8 m mit Insel 2 × 2 m über einen Steg (Gegenuhrzeigersinn, Loch im Uhrzeigersinn), wie der Kern ihn liefert. */
const ringMitInsel = [[0, 0], [10000, 0], [10000, 5000], [6000, 5000], [6000, 3000], [4000, 3000], [4000, 5000], [6000, 5000], [10000, 5000], [10000, 8000], [0, 8000]];

test('3D Boden: Ring mit Steg wird trianguliert, Fläche = Aussen − Insel, auf der Höhe z + 12 mm', () => {
  // Ring wie RaeumeAusWaenden.MitInseln: Steg von (10000, 5000) nach links zur Ecke (6000, 5000) der Insel, Insel im Uhrzeigersinn, Steg zurück
  const z = { raum: 'R', nachbar: 'erdreich', angrenzung: 'erdreich', art: 'boden', flaeche_m2: 76, polygon: ringMitInsel, z: 0 };
  const g = h3.huelleGeometrien([z]).get('erdreich');
  assert.ok(g.dreiecke >= 8, String(g.dreiecke));
  const pos = g.geometrie.getAttribute('position');
  for (let i = 0; i < pos.count; i++) assert.ok(Math.abs(pos.getY(i) - 0.012) < 1e-6);
  assert.ok(Math.abs(dreiecksFlaeche(g.geometrie) - 76) < 1e-3, String(dreiecksFlaeche(g.geometrie)));
  // Decke und Dach liegen 12 mm tiefer
  const d2 = h3.huelleGeometrien([{ ...z, angrenzung: 'aussen', art: 'dach', z: 2600 }]).get('aussen');
  assert.ok(Math.abs(d2.geometrie.getAttribute('position').getY(0) - 2.588) < 1e-6);
});

test('3D: eine Geometrie je Farbe, Dreieck → Zeile zugeordnet', () => {
  const zeilen = [
    seite(), seite({ angrenzung: 'beheizt', nachbar: 'R2' }), seite({ angrenzung: 'aussen' }),
    { raum: 'R', angrenzung: 'erdreich', art: 'boden', polygon: [[0, 0], [1000, 0], [1000, 1000], [0, 1000]], z: 0, nachbar: 'erdreich' },
  ];
  const g = h3.huelleGeometrien(zeilen);
  assert.deepEqual([...g.keys()], ['aussen', 'erdreich', 'unbeheizt', 'beheizt']);
  assert.deepEqual([...g.get('aussen').zeile], [0, 0, 2, 2]);
  assert.deepEqual([...g.get('beheizt').zeile], [1, 1]);
  assert.deepEqual([...g.get('erdreich').zeile], [3, 3]);
  assert.equal(g.get('unbeheizt').geometrie, null);
  const gruppe = h3.baueHuelle(zeilen);
  assert.equal(gruppe.children.length, 3);   // unbeheizt entfällt (keine Fläche)
  const m = gruppe.children.find((x) => x.userData.angrenzung === 'aussen');
  assert.equal(m.material.color.getHexString(), d.HUELLE_FARBEN.aussen.slice(1));
  assert.equal(m.material.opacity, d.HUELLE_DECKKRAFT_3D.aussen);
  assert.equal(m.material.transparent, true);
  assert.equal(m.material.depthWrite, false);
  assert.ok(m.userData.huelle && m.renderOrder === h3.RENDER_ORDER);
  const beheizt = gruppe.children.find((x) => x.userData.angrenzung === 'beheizt');
  assert.ok(beheizt.material.opacity < m.material.opacity, 'beheizt nur dezent');
  h3.huelleFreigeben(gruppe);
});

test('3D Treffer: Strahl auf die Fläche liefert die Raumgrenze, ins Loch der Insel nicht', () => {
  const zeilen = [
    { raum: 'R-I', nachbar: 'erdreich', angrenzung: 'erdreich', art: 'boden', polygon: ringMitInsel, z: 0, flaeche_m2: 76 },
    seite({ raum: 'R-I' }),
  ];
  const gruppe = h3.baueHuelle(zeilen);
  gruppe.updateMatrixWorld(true);
  const strahl = new THREE.Raycaster();
  const von = (x, y, z) => { strahl.set(new THREE.Vector3(x, y, z), new THREE.Vector3(0, -1, 0)); return h3.zeileAmTreffer(gruppe, strahl.intersectObjects(gruppe.children, false)); };
  // three: x = Ost, z = −Nord: Punkt (2 m, 4 m) im Raum
  const treffer = von(2, 5, -4);
  assert.ok(treffer);
  assert.equal(treffer.zeile.art, 'boden');
  assert.equal(treffer.index, 0);
  assert.equal(von(5, 5, -4), null, 'Insel (Loch) ist keine Fläche');
  // Strahl quer auf die Seitenwand (Süd, y = 0.012 m vor der Kante)
  strahl.set(new THREE.Vector3(3, 1.3, -2), new THREE.Vector3(0, 0, 1));
  const w = h3.zeileAmTreffer(gruppe, strahl.intersectObjects(gruppe.children, false));
  assert.equal(w.zeile.art, 'wand');
  assert.equal(w.index, 1);
  assert.equal(h3.zeileAmTreffer(gruppe, []), null);
  h3.huelleFreigeben(gruppe);
});

test('3D Umriss: Linienzug um die angeklickte Fläche (Viereck bei Seiten, Ring bei Böden)', () => {
  const l = h3.umrissLinie(seite(), 0x1e90ff);
  assert.equal(l.geometry.getAttribute('position').count, 4);
  assert.equal(l.raycast(), undefined);
  assert.equal(h3.umrissLinie({ art: 'wand' }, 0), null);
  assert.equal(h3.umrissEcken({ polygon: [[0, 0], [1, 1]], z: 0 }).length, 0);
});

test('3D Leistung: 10 000 Raumgrenzen in einer Sekunde zu vier Geometrien', () => {
  const zeilen = [];
  for (let i = 0; i < 10000; i++) {
    const x = (i % 100) * 7000, y = Math.floor(i / 100) * 5000;
    zeilen.push(i % 10 === 0
      ? { raum: 'R' + i, angrenzung: 'erdreich', art: 'boden', polygon: [[x, y], [x + 6000, y], [x + 6000, y + 4000], [x, y + 4000]], z: 0, nachbar: 'erdreich' }
      : seite({ raum: 'R' + i, angrenzung: ['aussen', 'beheizt', 'unbeheizt'][i % 3], punkte: [[x, y], [x + 4000, y]] }));
  }
  const t0 = performance.now();
  const gruppe = h3.baueHuelle(zeilen);
  const ms = performance.now() - t0;
  assert.ok(gruppe.children.length <= 4);
  assert.equal(gruppe.children.reduce((n, m) => n + m.userData.dreiecke, 0), 9000 * 2 + 1000 * 2);
  assert.ok(ms < 1000, `${Math.round(ms)} ms`);
  h3.huelleFreigeben(gruppe);
});

// ---------- Architekturkörper blasser ----------
test('Körper in der Darstellung Hülle: Deckkraft je Ebene vermindert, Kanten bleiben, Haustechnik unverändert', () => {
  const wand = { id: 'w', ebene: 'wand', teile: [{ art: 'quader', mitte: [0, 0, 1000], masse: [1000, 200, 2000], material: 'wand' }] };
  const decke = { id: 'd', ebene: 'decke', teile: [{ art: 'prisma', grundriss: [[0, 0], [1000, 0], [1000, 1000]], unten: 0, oben: 250, material: 'decke' }] };
  const rohr = { id: 'r', ebene: 'leitung', teile: [{ art: 'zylinder', von: [0, 0, 0], nach: [1000, 0, 0], radius: 50, material: 'rohr' }] };
  const normal = (k) => k.userData.materialien[0].opacity;
  const w0 = k3.baueKoerper(wand), w1 = k3.baueKoerper(wand, { blasser: k3.BLASS_HUELLE });
  assert.ok(Math.abs(normal(w1) - normal(w0) * k3.BLASS_HUELLE.wand) < 1e-9);
  assert.ok(w1.children.some((c) => c.userData.kante), 'Kanten der Wand bleiben');
  const d0 = k3.baueKoerper(decke), d1 = k3.baueKoerper(decke, { blasser: k3.BLASS_HUELLE });
  assert.equal(normal(d0), 1);
  assert.ok(normal(d1) < 0.3);
  assert.equal(normal(k3.baueKoerper(rohr, { blasser: k3.BLASS_HUELLE })), normal(k3.baueKoerper(rohr)));
});
