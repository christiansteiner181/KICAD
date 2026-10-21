// Tests der gedrehten Grundrisse (Nachbearbeitung N9) ohne Browser: Bildschirm-Transformation (gr-welt ↔ zuModell/zuBildschirm), Ausschnitt,
// Fangen und Winkelfang in den Achsen der Ansicht, Bemassung horizontal/vertikal, Texte der Werkzeugschicht, Zeilen der Eigenschaftenpalette.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/ausrichtung.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';

const A = await import('./ausrichtung.js');
const F = await import('../werkzeuge/fangen.js');
const P = await import('../werkzeuge/parameter.js');
const S = await import('../werkzeuge/schicht.js');
const L = await import('../werkzeuge/leitung-logik.js');
const E = await import('../oberflaeche/ausrichtung-palette.js');

const nah = (a, b, eps = 1e-6) => assert.ok(Math.abs(a - b) <= eps, `${a} ≠ ${b}`);
const punktGleich = (p, q, eps = 1e-6) => { nah(p[0], q[0], eps); nah(p[1], q[1], eps); };
const RAD = Math.PI / 180;

/** Matrix [a, b, c, d, e, f] eines SVG-transform aus scale(…) und rotate(…) (genau die zwei, die weltTransform schreibt). */
function matrix(transform) {
  let m = [1, 0, 0, 1, 0, 0];
  const mal = (n) => { m = [m[0] * n[0] + m[2] * n[1], m[1] * n[0] + m[3] * n[1], m[0] * n[2] + m[2] * n[3], m[1] * n[2] + m[3] * n[3], m[4], m[5]]; };
  for (const [, art, werte] of transform.matchAll(/(scale|rotate)\(([^)]*)\)/g)) {
    const z = werte.trim().split(/[\s,]+/).map(Number);
    if (art === 'scale') mal([z[0], 0, 0, z[1] ?? z[0]]);
    else { const w = z[0] * RAD; mal([Math.cos(w), Math.sin(w), -Math.sin(w), Math.cos(w)]); }
  }
  return m;
}
const anwenden = (m, p) => [m[0] * p[0] + m[2] * p[1] + m[4], m[1] * p[0] + m[3] * p[1] + m[5]];

// ============================================================================================== Bildschirm-Transformation

test('gr-welt: transform und zuModell/zuBildschirm rechnen dasselbe (Drehung 0, −37°, 30°)', () => {
  for (const d of [0, -37, 30, 123.4]) {
    const m = matrix(A.weltTransform(d));
    for (const p of [[0, 0], [1000, 0], [0, 1000], [-2500.5, 4321]]) {
      punktGleich(anwenden(m, p), A.svgAusModell(p, d), 1e-6);
      punktGleich(A.modellAusSvg(A.svgAusModell(p, d), d), p, 1e-6);
    }
  }
  assert.equal(A.weltTransform(0), 'scale(1 -1)');
  assert.equal(A.weltTransform(-37), 'scale(1 -1) rotate(-37)');
});

test('Drehung −37°: eine Kante unter 37° liegt auf dem Bildschirm waagrecht, Nord oben bei Himmelsrichtung', () => {
  const a = A.svgAusModell([0, 0], -37), b = A.svgAusModell([Math.cos(37 * RAD) * 10000, Math.sin(37 * RAD) * 10000], -37);
  nah(b[1] - a[1], 0, 1e-6);       // gleiche Bildschirmhöhe
  nah(b[0] - a[0], 10000, 1e-6);   // nach rechts
  // Himmelsrichtung, nordwinkel 30: geografisch Nord (30° im Uhrzeigersinn ab +y) zeigt nach oben (SVG y kleiner)
  const n = A.svgAusModell([Math.sin(30 * RAD), Math.cos(30 * RAD)], 30);
  punktGleich(n, [0, -1]);
});

test('Ausschnitt: Begrenzung in Ansichtsachsen vom Kern, sonst gerechnet; Box aus dem Modell umschliessend gedreht', () => {
  assert.deepEqual(A.ansichtsBegrenzung({ begrenzungAnsicht: [1, 2, 3, 4], begrenzung: [0, 0, 9, 9] }), { minx: 1, miny: 2, maxx: 3, maxy: 4 });
  assert.deepEqual(A.ansichtsBegrenzung({ begrenzung: [0, 0, 9, 9] }), { minx: 0, miny: 0, maxx: 9, maxy: 9 });
  // ohne begrenzungAnsicht, gedreht: Rechteck 10 × 6 m unter 37° → in Ansichtsachsen 10 × 6 m
  const ecken = [[0, 0], [10000, 0], [10000, 6000], [0, 6000]].map((p) => A.drehen(p, 37));
  const b = A.ansichtsBegrenzung({ drehung: -37, teile: [{ primitive: [{ art: 'polygon', punkte: ecken }, { art: 'text', punkte: [[99999, 99999]], text: 'x' }] }] });
  nah(b.maxx - b.minx, 10000, 1e-6); nah(b.maxy - b.miny, 6000, 1e-6);
  const box = A.boxInAnsicht({ minx: 0, miny: 0, maxx: 1000, maxy: 0.0001 }, 90);
  nah(box.minx, -0.0001, 1e-9); nah(box.maxy, 1000, 1e-9);
  assert.equal(A.ansichtsBegrenzung({ teile: [] }), null);
});

test('Nordpfeil, Anzeige der Ausrichtung', () => {
  assert.equal(A.nordCss(67), 'rotate(67deg)');
  assert.equal(A.nordCss(undefined), 'rotate(0deg)');
  assert.equal(A.ausrichtungText({ ausrichtung: 'geometrie', drehung: -37 }), 'Geometrie, gedreht −37.0°');
  assert.equal(A.ausrichtungText({ ausrichtung: 'himmelsrichtung', drehung: 0 }), 'Himmelsrichtung, nicht gedreht');
  assert.equal(A.ausrichtungText({ drehung: 0 }), '');
  nah(A.norm180(190), -170); nah(A.norm180(-180), 180);
});

// ============================================================================================== Fangen in den Achsen der Ansicht

const kanteUnter37 = { a: [0, 0], b: [Math.cos(37 * RAD) * 5000, Math.sin(37 * RAD) * 5000], id: 'w1' };
const daten = { punkte: [{ p: [0, 0], id: 'w1' }, { p: kanteUnter37.b, id: 'w1' }], kanten: [kanteUnter37] };

test('Fangen: Shift (orthogonal) in einer um −37° gedrehten Ansicht = parallel zur Gebäudekante unter 37°', () => {
  const maus = [Math.cos(37 * RAD) * 2000 + 40, Math.sin(37 * RAD) * 2000 - 60];   // leicht neben der Kante
  const f = F.fangen({ punkt: maus, daten: { punkte: [], kanten: [] }, radius: 10, letzter: [0, 0], ortho: true, drehung: -37 });
  assert.equal(f.ortho, true);
  nah(Math.atan2(f.punkt[1], f.punkt[0]) / RAD, 37, 1e-6);   // auf der Linie unter 37°
  // ohne Drehung bleibt es wie bisher: waagrecht im Modell
  const g = F.fangen({ punkt: [2000, 30], daten: { punkte: [], kanten: [] }, radius: 10, letzter: [0, 0], ortho: true });
  assert.deepEqual(g.punkt, [2000, 0]);
});

test('Fangen: Raster in den Ansichtsachsen, Endpunkte und Kanten unverändert aus dem Modell', () => {
  const r = F.fangen({ punkt: A.drehen([1234, 567], 37), daten: { punkte: [], kanten: [] }, radius: 1, raster: 100, drehung: -37 });
  assert.equal(r.art, 'raster');
  const v = A.drehen(r.punkt, -37);   // in Ansichtsachsen ein Vielfaches von 100
  nah(v[0], 1200, 1e-5); nah(v[1], 600, 1e-5);

  const e = F.fangen({ punkt: [kanteUnter37.b[0] + 3, kanteUnter37.b[1] - 2], daten, radius: 10, drehung: -37 });
  assert.equal(e.art, 'endpunkt');
  punktGleich(e.punkt, kanteUnter37.b, 1e-6);
  const m = F.fangen({ punkt: [kanteUnter37.b[0] / 2 + 2, kanteUnter37.b[1] / 2], daten, radius: 10, drehung: -37 });
  assert.equal(m.art, 'mitte');
  assert.equal(m.kante, kanteUnter37);   // das Original der Fangdaten, nicht die gedrehte Kopie
});

test('Winkelfang der Leitungen: Achsrichtungen der Ansicht', () => {
  assert.deepEqual(L.richtungsKandidaten({ drehung: -37 }), [37, 127, 217, 307]);
  assert.deepEqual(L.richtungsKandidaten({}), [0, 90, 180, 270]);
  const w = L.winkelFang({ letzter: [0, 0], maus: A.drehen([1000, 20], 37), kandidaten: L.richtungsKandidaten({ drehung: -37 }) });
  nah(w.winkel, 37);
});

test('Bemassung: horizontal/vertikal in den Achsen der Ansicht, ohne Drehung wie bisher', () => {
  punktGleich(P.messrichtung([[0, 0], [1000, 1000]], 'horizontal'), [1, 0]);
  const d = P.messrichtung([[0, 0], A.drehen([1000, 300], 37)], 'horizontal', -37);
  punktGleich(d, [Math.cos(37 * RAD), Math.sin(37 * RAD)]);
  const v = P.messrichtung([[0, 0], A.drehen([300, -1000], 37)], 'vertikal', -37);
  punktGleich(v, A.drehen([0, -1], 37));
  nah(P.bemassungVersatz([[0, 0], A.drehen([1000, 0], 37)], 'horizontal', A.drehen([500, 400], 37), -37), 400, 0.1);
  const par = P.bemassungParameter({ ansicht: 'AN-1', punkte: [[0, 0], A.drehen([1000, 0], 37)], richtung: 'horizontal', lage: A.drehen([500, -250], 37), drehung: -37 });
  nah(par.eigenschaften.versatz, -250, 0.1);
});

// ============================================================================================== Werkzeugschicht

test('Werkzeugschicht: Texte bleiben auf dem Bildschirm waagrecht, Masse folgen dem Segment, Winkel in Ansichtsachsen', () => {
  try {
    S.drehungSetzen(-37);
    // waagrechter Text: in der gedrehten Gruppe um +37° gegengedreht (SVG rotate(−37) nach scale(1 −1))
    assert.match(S.text([0, 0], 'x', 1), /scale\(1 -1\) rotate\(-37\)/);
    // Versatz in Bildschirmpixeln nach rechts → im Modell entlang 37°
    const t = S.text([0, 0], 'x', 1, { versatzPx: [10, 0] });
    const [, x, y] = t.match(/translate\(([-\d.]+) ([-\d.]+)\)/).map(Number);
    nah(Math.atan2(y, x) / RAD, 37, 1e-3);
    // Masszahl entlang eines Segments unter 37°: auf dem Bildschirm waagrecht, Winkelangabe 0.0°
    const m = S.tempMass([0, 0], A.drehen([2000, 0], 37), 1);
    assert.match(m, /2000 mm {2}0\.0°/);
    assert.match(m, /scale\(1 -1\) rotate\(-37\)/);
    // Fangsymbol gegengedreht (Quadrat bleibt achsparallel)
    assert.match(S.fangsymbol({ punkt: [10, 20], art: 'endpunkt' }, 1), /<g transform="rotate\(37 10 20\)">/);
  } finally { S.drehungSetzen(0); }
  assert.doesNotMatch(S.text([0, 0], 'x', 1), /rotate/);
  assert.doesNotMatch(S.fangsymbol({ punkt: [10, 20], art: 'endpunkt' }, 1), /<g transform/);
});

// ============================================================================================== Navigationswürfel (N11) am Planwinkel

test('Würfel: VORNE schaut auf die lange Gebäudekante wie der gerade Grundriss (θ = −planwinkel)', async () => {
  const W = await import('./wuerfel-logik.js');
  for (const kante of [37, -20, 127, 0]) {
    const pw = (((-kante % 90) + 90) % 90) > 45 ? (((-kante % 90) + 90) % 90) - 90 : ((-kante % 90) + 90) % 90;   // wie Planwinkel.AusRichtung
    const theta = W.gebaeudeAusrichtungGrad({ werte: { planwinkel: pw } }) * RAD;
    const vorne = W.lokalZuWelt({ x: 0, y: 0, z: 1 }, theta);   // three: x Ost, z −Nord
    const vorneModell = [vorne.x, -vorne.z];
    // liegt die Kante im geraden Grundriss waagrecht (37°, −20°, 0°), schaut VORNE senkrecht auf sie; steht sie dort senkrecht
    // (127°: Drehung bleibt in −45 … 45°), schaut VORNE wie der Grundriss auf die Schmalseite
    const waagrecht = Math.abs(Math.sin((kante + pw) * RAD)) < 1e-9;
    nah(Math.abs(vorneModell[0] * Math.cos(kante * RAD) + vorneModell[1] * Math.sin(kante * RAD)), waagrecht ? 0 : 1, 1e-9);
    // und genau dorthin, wo im geraden Grundriss (Drehung pw) «unten» ist
    punktGleich(A.svgAusModell(vorneModell, pw).map((v) => Math.round(v * 1e9) / 1e9), [0, 1], 1e-9);
  }
  // ohne gespeicherten Planwinkel: der vom Kern gerechnete; ohne beides 0
  assert.equal(W.gebaeudeAusrichtungGrad({ werte: {} }, -37), 37);
  assert.equal(W.gebaeudeAusrichtungGrad({ werte: { planwinkel: 10 } }, -37), -10);
  assert.equal(W.gebaeudeAusrichtungGrad({ werte: {} }, null), 0);
});

// ============================================================================================== Eigenschaftenpalette

const H = { zeile: (l, f) => `<z l="${l}">${f}</z>`, nurLesen: (t) => `<t>${t}</t>` };

test('Palette: Drehung und Quelle der Ansicht, Knopf «Planwinkel neu bestimmen» am Gebäude', () => {
  const daten = {
    gebaeude: [{ gebaeude: 'GEB', planwinkel: -37, bestimmt: { winkel: -37, wand: 'W-1', laenge_m: 10, aussenwand: true, quelle: 'abgeleitet: …' } }],
    ansicht: { kennzeichen: 'AN-EG', ausrichtung: 'geometrie', drehung: -37, quelle: 'GEB.planwinkel −37° – abgeleitet', hinweis: 'Kein Nordwinkel …' },
  };
  assert.equal(E.drehungText(daten.ansicht), '−37.0° · Geometrie (längste Gebäudekante waagrecht)');
  const z = E.ausrichtungZeilen(daten, H).join('');
  assert.match(z, /Drehung/); assert.match(z, /−37\.0°/); assert.match(z, /Kein Nordwinkel/);
  const g = E.planwinkelZeilen(daten, 'GEB', H).join('');
  assert.match(g, /−37\.0° aus W-1 \(Aussenwand, 10\.00 m\)/);
  assert.match(g, /class="ep-pw-knopf" data-gebaeude="GEB"/);
  assert.deepEqual(E.ausrichtungZeilen(null, H), []);
  assert.match(E.planwinkelZeilen(null, 'GEB', H).join(''), /Planwinkel neu bestimmen/);
});
