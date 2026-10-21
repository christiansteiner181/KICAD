// Tests von N22 (Begehen: Wände undurchsichtig, durchscheinend nur im Durchgang näher als 500 mm) – ohne Browser:
// Abstandsrechnung in begehen-logik.js (Punkt–Prisma, Quader, Zylinder, Hüllbox-Vorauswahl), Umschalten und Wiederherstellen in durchgang3d.js.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/durchgang.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { register } from 'node:module';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const threeUrl = pathToFileURL(path.resolve(hier, '../../lib/three/three.module.js')).href;
register('data:text/javascript,' + encodeURIComponent(
  `export async function resolve(s, c, n) { if (s === 'three') return { url: ${JSON.stringify(threeUrl)}, shortCircuit: true }; return n(s, c); }`));

const L = await import('./begehen-logik.js');
const { erzeugeDurchgang } = await import('./durchgang3d.js');
const k3 = await import('./koerper3d.js');
const THREE = await import(threeUrl);

const { DURCHGANG_MM, DURCHGANG_DECKKRAFT, formAusTeil, huellboxVon, abstandZurForm, abstandZumKoerper, imDurchgang, abstandZurFlaeche, durchgangDeckkraft } = L;
const nah = (a, b, eps = 1e-9, t = '') => assert.ok(Math.abs(a - b) < eps, `${t} ${a} ≠ ${b}`);

/** Wand 4 m lang, 200 mm dick, 0 … 2.5 m hoch, entlang x bei y = 0 … 200 (Projekt-mm) → three: z von 0 bis −0.2. */
const wand = { art: 'prisma', grundriss: [[0, 0], [4000, 0], [4000, 200], [0, 200]], unten: 0, oben: 2500, material: 'wand' };
/** Diagonale Wand von (0,0) nach (4000,4000), 200 mm dick. */
const schraeg = { art: 'prisma', grundriss: [[0, 0], [4000, 4000], [3858.6, 4141.4], [-141.4, 141.4]], unten: 0, oben: 2500, material: 'wand' };
const pt = (x, y, z) => ({ x, y, z });   // three-Meter

test('Konstante: 500 mm (Christian 21.10.2026), Deckkraft im Durchgang', () => {
  assert.equal(DURCHGANG_MM, 500);
  assert.equal(durchgangDeckkraft(0.28), 0.28);   // bisherige durchscheinende Deckkraft bleibt
  assert.equal(durchgangDeckkraft(1), DURCHGANG_DECKKRAFT);   // undurchsichtige Decke: Vorgabe
  assert.equal(durchgangDeckkraft(undefined), DURCHGANG_DECKKRAFT);
});

// ---------------------------------------------------------------------------------------------- Abstand

test('Abstand Punkt–Prisma: innen 0, seitlich, über/unter, Ecke', () => {
  const f = formAusTeil(wand);
  nah(abstandZurForm(pt(2, 1.2, -0.1), f), 0, 1e-9, 'innen');
  nah(abstandZurForm(pt(2, 1.2, 0.3), f), 0.3, 1e-9, 'seitlich vorn');
  nah(abstandZurForm(pt(2, 1.2, -0.5), f), 0.3, 1e-9, 'seitlich hinten (z = −0.2 Rückseite)');
  nah(abstandZurForm(pt(2, 3.0, -0.1), f), 0.5, 1e-9, 'über OK');
  nah(abstandZurForm(pt(2, -0.4, -0.1), f), 0.4, 1e-9, 'unter UK');
  nah(abstandZurForm(pt(-0.3, 1.2, 0.4), f), 0.5, 1e-9, 'Grundrissecke (Pythagoras 0.3/0.4)');
  nah(abstandZurForm(pt(-0.3, 3.5, 0.4), f), Math.hypot(0.3, 0.4, 1.0), 1e-9, 'Ecke dreidimensional');
});

test('Abstand: schräge Wand wird gegen den Körper gemessen, nicht gegen die Hüllbox', () => {
  const f = formAusTeil(schraeg);
  // Punkt (3000, 1000) mm: im achsparallelen Hüllquader (0 … 4000 × −141 … 4141), aber 1.41 m von der Diagonalen entfernt
  const p = pt(3, 1.2, -1);
  assert.equal(abstandZurBox0(f, p), 0, 'die Hüllbox sagt «innen»');
  const d = abstandZurForm(p, f);
  assert.ok(d > 1.2 && d < 1.5, `Abstand ${d}`);
  nah(d, 2 / Math.SQRT2, 0.01, 'Abstand zur Wandfläche (Diagonale), Wand liegt auf der anderen Seite');
  assert.equal(imDurchgang(p, [f], huellboxVon([f])), false);
  // auf der Wand selbst: Mitte bei (2000, 2000)
  assert.equal(imDurchgang(pt(2, 1.2, -2), [f], huellboxVon([f])), true);
  nah(abstandZurForm(pt(2, 1.2, -2), f), 0);
});
const abstandZurBox0 = (f, p) => L.abstandZurBox(p, f.box);

test('Wand mit Loch (Fenster als Aussparung im Grundriss): im Loch Abstand zum Lochrand', () => {
  const f = formAusTeil({ art: 'prisma', grundriss: [[0, 0], [4000, 0], [4000, 4000], [0, 4000]], loecher: [[[1000, 1000], [3000, 1000], [3000, 3000], [1000, 3000]]], unten: 0, oben: 2500 });
  nah(abstandZurFlaeche(0.5, -0.5, f.ring, f.loecher), 0);          // im Wandstreifen
  nah(abstandZurFlaeche(2, -2, f.ring, f.loecher), 1);              // Mitte des Lochs: 1 m bis zum Rand
  nah(abstandZurFlaeche(5, -2, f.ring, f.loecher), 1);              // ausserhalb: 1 m
});

test('Quader mit Drehung, Zylinder, Kugel, Netzkörper', () => {
  // Quader 4000 × 200 × 2500 um (0,0) gedreht 90°: liegt dann entlang y (Nord), three-z
  const q = formAusTeil({ art: 'quader', mitte: [0, 0, 1250], masse: [4000, 200, 2500], drehung: 90 });
  nah(abstandZurForm(pt(0, 1.25, -1.9), q), 0, 1e-9, 'innen (entlang Nord)');
  nah(abstandZurForm(pt(1.1, 1.25, 0), q), 1.0, 1e-9, 'seitlich 1.1 − 0.1');
  nah(abstandZurForm(pt(0, 1.25, -2.4), q), 0.4, 1e-9, 'Stirnseite');
  const z = formAusTeil({ art: 'zylinder', von: [0, 0, 0], nach: [0, 0, 3000], radius: 150 });
  nah(abstandZurForm(pt(0.65, 1.5, 0), z), 0.5, 1e-9, 'Stütze rund');
  nah(abstandZurForm(pt(0, 3.5, 0), z), 0.5, 1e-9, 'über der Stütze');
  nah(abstandZurForm(pt(0, 1.5, 0), z), 0, 1e-9, 'in der Stütze');
  const k = formAusTeil({ art: 'kugel', mitte: [0, 0, 0], radius: 500 });
  nah(abstandZurForm(pt(1, 0, 0), k), 0.5);
  const n = formAusTeil({ art: 'koerper', ecken: [[0, 0, 0], [1000, 0, 0], [0, 1000, 0], [0, 0, 1000]], dreiecke: [[0, 1, 2]] });
  nah(abstandZurForm(pt(2, 0.5, 0), n), 1);
  assert.equal(formAusTeil({ art: 'netz', quelle: 'x' }), null);
  assert.equal(formAusTeil({ art: 'prisma', grundriss: [[0, 0], [1, 1]], unten: 0, oben: 1 }), null);
});

test('Schwelle 500 mm: darunter durchscheinend, ab 500 mm nicht', () => {
  const f = [formAusTeil(wand)], box = huellboxVon(f);
  assert.equal(imDurchgang(pt(2, 1.7, 0.4999), f, box), true);
  assert.equal(imDurchgang(pt(2, 1.7, 0.5), f, box), false);
  assert.equal(imDurchgang(pt(2, 1.7, 0.5001), f, box), false);
  assert.equal(imDurchgang(pt(2, 1.7, -0.1), f, box), true);   // mitten in der Wand
  assert.equal(imDurchgang(pt(2, 1.7, -0.6999), f, box), true);   // hinten 0.4999 vom Rand
  assert.equal(imDurchgang(pt(2, 1.7, -0.7001), f, box), false);
  assert.equal(imDurchgang(pt(2, 3.2, 0), f, box), false, 'über der Wand: 0.7 m');
  assert.equal(imDurchgang(pt(2, 1.7, 0.4), [], null), false, 'ohne Formen nie');
});

test('Vorauswahl über die Hüllbox spart die genaue Rechnung, ändert aber das Ergebnis nicht', () => {
  const f = [formAusTeil(schraeg)], box = huellboxVon(f);
  for (const [x, z] of [[3, -1], [2, -2], [-2, 0], [5, -5], [1, -1.2], [1.35, -1.5]]) {
    const p = pt(x, 1.2, z);
    assert.equal(imDurchgang(p, f, box), imDurchgang(p, f, null), `Punkt ${x}/${z}`);
  }
  assert.equal(abstandZumKoerper(pt(100, 1, 100), f, box, 0.5), Infinity);
});

// ---------------------------------------------------------------------------------------------- Umschalten und Wiederherstellen

const material = (deckkraft) => ({ opacity: deckkraft, transparent: deckkraft < 1, depthWrite: deckkraft >= 1, clippingPlanes: ['E1', 'E2'], needsUpdate: false });
function gruppe(id, ebene, teil, deckkraft) {
  const formen = [formAusTeil(teil)];
  const m = material(deckkraft);
  return { m, g: { userData: { id, ebene, materialien: [m], formen, huellbox: huellboxVon(formen) } } };
}
function aufbau() {
  const w1 = gruppe('W1', 'wand', wand, 0.28);                                                // vordere Wand
  const w2 = gruppe('W2', 'wand', { ...wand, grundriss: [[0, 3000], [4000, 3000], [4000, 3200], [0, 3200]] }, 0.28);   // hintere Wand bei y = 3 m
  const decke = gruppe('D1', 'decke', { art: 'prisma', grundriss: [[0, 0], [4000, 0], [4000, 3200], [0, 3200]], unten: 2500, oben: 2800 }, 1);
  const leitung = gruppe('L1', 'leitung', { art: 'zylinder', von: [0, 1000, 2000], nach: [4000, 1000, 2000], radius: 50 }, 0.6);
  const raum = gruppe('R1', 'raum', wand, 0.15);
  const alle = [w1, w2, decke, leitung, raum];
  const kamera = { position: { x: 2, y: 1.7, z: -1.5 } };   // Raumitte, 1.5 m von beiden Wänden
  let angefordert = 0, kontrolle = null;
  const d = erzeugeDurchgang({ bauteile: () => alle.map((a) => a.g), kamera, anfordern: () => { angefordert++; }, beiAenderung: (z) => { kontrolle = z; } });
  return { w1, w2, decke, leitung, raum, alle, kamera, d, get angefordert() { return angefordert; }, get kontrolle() { return kontrolle; } };
}

test('Start: Wände und Decken undurchsichtig; Leitung und Raum bleiben', () => {
  const a = aufbau();
  a.d.starten();
  for (const x of [a.w1, a.w2, a.decke]) { assert.equal(x.m.opacity, 1); assert.equal(x.m.transparent, false); assert.equal(x.m.depthWrite, true); }
  assert.equal(a.leitung.m.opacity, 0.6); assert.equal(a.leitung.m.transparent, true);
  assert.equal(a.raum.m.opacity, 0.15); assert.equal(a.raum.m.transparent, true);
  assert.equal(a.kontrolle.bauteile, 3);
  assert.equal(a.kontrolle.undurchsichtig, 3);
  assert.deepEqual(a.kontrolle.nah, []);
  assert.equal(a.w1.m.needsUpdate, true, 'transparent → opak braucht neu übersetzte Shader');
});

test('Hineingehen: Wand durchscheinend (bisherige Deckkraft), danach weiter: wieder undurchsichtig', () => {
  const a = aufbau();
  a.d.starten();
  a.kamera.position.z = -0.4;   // 0.4 m vor der Wand W1 (z von 0 bis −0.2 → gleich Abstand 0.2 … Kamera bei −0.4: 0.2 m hinter der Rückseite)
  a.d.pruefen();
  assert.equal(a.w1.m.opacity, 0.28); assert.equal(a.w1.m.transparent, true); assert.equal(a.w1.m.depthWrite, false);
  assert.equal(a.w2.m.opacity, 1, 'die hintere Wand ist weit weg');
  assert.deepEqual(a.kontrolle.nah.map((n) => n.id), ['W1']);
  a.kamera.position.z = 0.3;    // mitten im Schritt: vor der Wand, 0.3 m
  a.d.pruefen();
  assert.equal(a.w1.m.opacity, 0.28);
  a.kamera.position.z = 0.6;    // wieder mehr als 0.5 m
  a.d.pruefen();
  assert.equal(a.w1.m.opacity, 1); assert.equal(a.w1.m.transparent, false); assert.equal(a.w1.m.depthWrite, true);
  assert.deepEqual(a.kontrolle.nah, []);
});

test('Decke (normal undurchsichtig) wird im Durchgang mit der Vorgabe durchscheinend, Höhenabstand zählt', () => {
  const a = aufbau();
  a.d.starten();
  a.kamera.position.y = 2.2;    // 0.3 m unter OK Decke-Unterkante (2.5)
  a.d.pruefen();
  assert.equal(a.decke.m.opacity, DURCHGANG_DECKKRAFT); assert.equal(a.decke.m.transparent, true); assert.equal(a.decke.m.depthWrite, false);
  a.kamera.position.y = 1.7;
  a.d.pruefen();
  assert.equal(a.decke.m.opacity, 1);
});

test('Beenden stellt genau die vorherige Darstellung her, Clipping-Ebenen bleiben unberührt', () => {
  const a = aufbau();
  const ebenen = a.w1.m.clippingPlanes;
  a.d.starten();
  a.kamera.position.z = -0.1;
  a.d.pruefen();
  a.d.beenden();
  for (const [x, o, t, w] of [[a.w1, 0.28, true, false], [a.w2, 0.28, true, false], [a.decke, 1, false, true], [a.leitung, 0.6, true, false], [a.raum, 0.15, true, false]]) {
    assert.equal(x.m.opacity, o); assert.equal(x.m.transparent, t); assert.equal(x.m.depthWrite, w);
    assert.equal(x.m.clippingPlanes, x === a.w1 ? ebenen : x.m.clippingPlanes);
  }
  assert.equal(a.w1.m.clippingPlanes, ebenen);
  assert.deepEqual(ebenen, ['E1', 'E2']);
  assert.equal(a.kontrolle, null);
  assert.equal(a.d.aktiv, false);
  // nach dem Beenden ändert eine Bewegung nichts mehr
  a.kamera.position.z = 0.1; a.d.pruefen();
  assert.equal(a.w1.m.opacity, 0.28);
});

test('Ohne Bewegung wird nicht neu gerechnet; neue Körper werden nachträglich undurchsichtig, entfernte vergessen', async () => {
  const a = aufbau();
  let rechnungen = 0;
  const alle = a.alle.map((x) => x.g);
  const d = erzeugeDurchgang({ bauteile: () => { rechnungen++; return alle; }, kamera: a.kamera, anfordern() {}, beiAenderung() {} });
  d.starten();
  const n0 = rechnungen;
  d.pruefen(); d.pruefen();
  assert.equal(rechnungen, n0, 'gleiche Lage → keine Rechnung');
  const neu = gruppe('W3', 'wand', { ...wand, grundriss: [[0, -3000], [4000, -3000], [4000, -2800], [0, -2800]] }, 0.28);
  alle.push(neu.g);
  d.neueObjekte();
  await Promise.resolve();   // gesammelte Bewertung (Mikroaufgabe)
  assert.equal(neu.m.opacity, 1, 'neuer Körper undurchsichtig');
  assert.equal(d.zustand().bauteile, 4);
  alle.splice(alle.indexOf(neu.g), 1);
  d.neueObjekte();
  await Promise.resolve();
  assert.equal(d.zustand().bauteile, 3);
  d.beenden();
});

test('Zweiter Start (anderer Raum) merkt die ursprünglichen Werte nicht neu', () => {
  const a = aufbau();
  a.d.starten();
  a.kamera.position.z = -0.1; a.d.pruefen();
  a.d.starten();
  a.d.beenden();
  assert.equal(a.w1.m.opacity, 0.28); assert.equal(a.w1.m.transparent, true); assert.equal(a.w1.m.depthWrite, false);
});

test('Echte Körper aus baueKoerper: Formen und Hüllbox, Umschalten auf echten three-Materialien', () => {
  const g = k3.baueKoerper({ id: 'W9', ebene: 'wand', teile: [{ ...wand, deckkraft: 0.3 }] });
  assert.equal(g.userData.formen.length, 1);
  assert.equal(g.userData.formen[0].art, 'poly');
  assert.equal(g.userData.huellbox.length, 6);
  assert.ok(g.userData.materialien[0] instanceof THREE.Material);
  const kamera = { position: { x: 2, y: 1.7, z: 3 } };
  const d = erzeugeDurchgang({ bauteile: () => [g], kamera, anfordern() {} });
  d.starten();
  const m = g.userData.materialien[0];
  assert.equal(m.opacity, 1); assert.equal(m.transparent, false); assert.equal(m.depthWrite, true);
  kamera.position.z = 0.2; d.pruefen();
  assert.equal(m.opacity, 0.3); assert.equal(m.transparent, true);
  d.beenden();
  assert.equal(m.opacity, 0.3); assert.equal(m.transparent, true); assert.equal(m.depthWrite, false);
  k3.freigeben(g);
});

test('Leistung: 5000 Bauteile, Bewegung bleibt weit unter einem Bild (16 ms)', () => {
  const gr = [];
  for (let i = 0; i < 5000; i++) {
    const x = (i % 100) * 6000, y = Math.floor(i / 100) * 6000;
    gr.push(gruppe('W' + i, 'wand', { art: 'prisma', grundriss: [[x, y], [x + 4000, y + 100], [x + 4000, y + 300], [x, y + 200]], unten: 0, oben: 2500 }, 0.28).g);
  }
  const kamera = { position: { x: 3, y: 1.7, z: -3 } };
  const d = erzeugeDurchgang({ bauteile: () => gr, kamera, anfordern() {} });
  d.starten();
  const t0 = performance.now();
  for (let i = 0; i < 100; i++) { kamera.position.x = 3 + i * 0.1; d.pruefen(); }
  const je = (performance.now() - t0) / 100;
  assert.ok(je < 16, `je Bewegung ${je.toFixed(2)} ms`);
  d.beenden();
});
