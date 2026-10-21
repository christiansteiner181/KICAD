// Tests von Modul XR, Teil XR-H (Sitzungsraum mit Modell auf dem Tisch) – der Teil ohne Brille und ohne Browser: xr-sitzung-logik.js
// (Einpassen 1:50, Zoomgrenzen, Drehen mit einer Hand, Zoom mit zwei Händen um den Mittelpunkt, Schnitthöhe unter und über der Decke, «Alle Etagen», Zurücksetzen, Tablet-Seite).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/xr-sitzung.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';

const S = await import('./xr-sitzung-logik.js');
const nah = (a, b, eps = 1e-9, t = '') => assert.ok(Math.abs(a - b) < eps, `${t} ${a} ≠ ${b}`);
const GRAD = Math.PI / 180;

// ------------------------------------------------------------------------------------------------ Einpassen und Grenzen
test('Einpassen: zu Beginn 1:50 in der Mitte der Platte, Drehung 0', () => {
  const l = S.einpassen({ b: 12.8, t: 8.8, h: 9 });
  assert.equal(l.massstab, 50); assert.equal(l.drehung, 0); assert.equal(l.x, 0); assert.equal(l.z, 0); assert.equal(l.passt, true);
  // 12.8 m / 50 = 0.256 m: passt auf die nutzbare Platte (1.4 × 0.8 m)
  const p = S.platte(); nah(p.b, 1.4); nah(p.t, 0.8);
  assert.ok(12.8 / l.massstab <= p.b && 8.8 / l.massstab <= p.t);
});

test('Einpassen: ein grosses Modell bekommt einen kleineren Massstab, höchstens 1:200', () => {
  const gross = S.einpassen({ b: 100, t: 30, h: 20 });                 // 100 m / 50 = 2 m: zu breit für 1.4 m
  assert.equal(gross.massstab, Math.ceil(100 / 1.4)); assert.ok(100 / gross.massstab <= 1.4 + 1e-9); assert.equal(gross.passt, true);
  const riesig = S.einpassen({ b: 600, t: 40, h: 10 });
  assert.equal(riesig.massstab, S.MASSSTAB.max); assert.equal(riesig.passt, false);
  const tief = S.einpassen({ b: 10, t: 60, h: 10 });                   // Tiefe 60 m / 0.8 m
  assert.equal(tief.massstab, 75); assert.ok(60 / tief.massstab <= 0.8 + 1e-9);
  for (const u of [null, { b: 0, t: 0 }, { b: NaN, t: 3 }]) assert.equal(S.einpassen(u).massstab, 50, 'ohne gültige Grösse 1:50');
});

test('Zoomgrenzen: 1:20 bis 1:200, ungültige Werte fallen auf 1:50', () => {
  assert.equal(S.massstabBegrenzen(5), 20); assert.equal(S.massstabBegrenzen(1000), 200); assert.equal(S.massstabBegrenzen(80), 80);
  assert.equal(S.massstabBegrenzen(NaN), 50); assert.equal(S.massstabBegrenzen(undefined), 50);
  assert.equal(S.massstabText(50), '1:50'); assert.equal(S.massstabText(49.6), '1:50'); assert.equal(S.massstabText(3), '1:20');
  // Die Mitte des Modells bleibt auf der Platte
  const l = S.lageBegrenzen({ massstab: 50, drehung: 0, x: 5, z: -5 });
  nah(l.x, S.TISCH.breite / 2 - S.TISCH.halt); nah(l.z, -(S.TISCH.tiefe / 2 - S.TISCH.halt));
});

// ------------------------------------------------------------------------------------------------ Griffe
test('Griffe: links und rechts der Modellmitte, GRIFF_ABSTAND_M ausserhalb der Modellbreite, GRIFF_HOEHE_M über der Platte; sie folgen Lage, Drehung und Massstab', () => {
  const G = { b: 12, t: 8, h: 9 };
  const l = S.einpassen(G);                                   // 1:50, Mitte der Platte
  const m = S.modellMitte(l);
  let p = S.griffPunkte(l, G);
  assert.equal(p.length, 2); assert.equal(p[0].seite, 'links'); assert.equal(p[1].seite, 'rechts');
  const halb = 12 / 50 / 2 + S.GRIFF_ABSTAND_M;
  nah(p[0].x, m.x - halb); nah(p[1].x, m.x + halb); nah(p[0].z, m.z); nah(p[1].z, m.z);
  nah(p[0].y, S.TISCH.hoehe + S.GRIFF_HOEHE_M); nah(p[1].y, S.TISCH.hoehe + S.GRIFF_HOEHE_M);
  assert.equal(S.GRIFF_ABSTAND_M, 0.06); assert.equal(S.GRIFF_HOEHE_M, 0.05); assert.equal(S.GRIFF_FANG_M, 0.05); assert.equal(S.GRIFF_HOVER_M, 0.05); assert.equal(S.GRIFF_DURCHMESSER_M, 0.025);
  // Zoom auf 1:25: die Modellbreite verdoppelt sich, die Griffe rücken mit (der Abstand von 6 cm bleibt)
  p = S.griffPunkte({ ...l, massstab: 25 }, G);
  nah(p[1].x - S.modellMitte(l).x, 12 / 25 / 2 + S.GRIFF_ABSTAND_M);
  // Verschiebung: die Griffe folgen der Modellmitte
  p = S.griffPunkte({ ...l, x: 0.1, z: -0.05 }, G);
  nah((p[0].x + p[1].x) / 2, m.x + 0.1); nah((p[0].z + p[1].z) / 2, m.z - 0.05);
  // Drehung um 90° (gegen den Uhrzeigersinn von oben): der rechte Griff liegt bei −z (in Blickrichtung des Raums hinten), der linke bei +z; die Höhe bleibt
  p = S.griffPunkte({ ...l, drehung: 90 * GRAD }, G);
  nah(p[1].x, m.x, 1e-9); nah(p[1].z, m.z - halb, 1e-9); nah(p[0].z, m.z + halb, 1e-9); nah(p[1].y, S.TISCH.hoehe + S.GRIFF_HOEHE_M);
  // winziges oder unbekanntes Modell: Mindestabstand, keine NaN
  for (const u of [null, { b: 0 }, { b: NaN }]) { const q = S.griffPunkte(l, u); assert.ok(q.every((x) => Number.isFinite(x.x)), 'endlich'); nah(q[1].x - m.x, S.GRIFF_MIN_HALB_M + S.GRIFF_ABSTAND_M); }
});

test('Griff-Treffer: ein Pinch in höchstens 5 cm fasst den nächsten Griff, weiter weg nichts; Controller zielen mit dem Strahl', () => {
  const l = S.einpassen({ b: 12, t: 8, h: 9 });
  const gp = S.griffPunkte(l, { b: 12, t: 8, h: 9 });
  const bei = (g, dx, dy = 0, dz = 0) => ({ x: g.x + dx, y: g.y + dy, z: g.z + dz });
  assert.equal(S.griffTreffer(bei(gp[0], 0.04), null, gp), 0);
  assert.equal(S.griffTreffer(bei(gp[1], 0, 0.03, 0.03), null, gp), 1, '3D-Abstand √(0.03²+0.03²) = 4.2 cm');
  assert.equal(S.griffTreffer(bei(gp[0], 0.051), null, gp), null, 'knapp ausserhalb');
  assert.equal(S.griffTreffer(bei(gp[0], 0, 0.06), null, gp), null, 'zu hoch');
  assert.equal(S.griffTreffer(bei(gp[0], -0.3), null, gp), null, 'weit weg');
  assert.equal(S.griffTreffer(null, null, gp), null);
  assert.equal(S.griffTreffer({ x: (gp[0].x + gp[1].x) / 2, y: gp[0].y, z: gp[0].z }, null, gp), null, 'in der Mitte des Modells: weder noch');
  // Controller: Strahl von 0.5 m entfernt, auf den rechten Griff gerichtet
  const u = bei(gp[1], 0, 0.3, 0.5), r = { x: gp[1].x - u.x, y: gp[1].y - u.y, z: gp[1].z - u.z };
  assert.equal(S.griffTreffer(u, { u, r }, gp), 1, 'Zielstrahl trifft');
  assert.equal(S.griffTreffer(u, null, gp), null, 'ohne Strahl ist die Hand zu weit weg');
  assert.equal(S.griffTreffer(u, { u, r: { x: -r.x, y: -r.y, z: -r.z } }, gp), null, 'Strahl zeigt weg');
  nah(S.strahlAbstand({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: -1 }, { x: 0.03, y: 0, z: -2 }), 0.03);
  nah(S.strahlAbstand({ x: 0, y: 0, z: 0 }, { x: 0, y: 0, z: -1 }, { x: 0, y: 0, z: 1 }), 1, 1e-9, 'hinter dem Ursprung zählt der Abstand zum Ursprung');
});

// ------------------------------------------------------------------------------------------------ Einhand: starr an der Hand
const hand1 = (x, z, gieren = null) => ({ griff: true, nah: true, x, z, gieren });

test('Eine Hand: das Modell folgt der Hand (Verschieben) und dreht sich mit ihr um die Senkrechte', () => {
  const l0 = { massstab: 50, drehung: 0, x: 0, z: 0 };
  const mitte = S.modellMitte(l0);
  const start = S.einhandStart(l0, { x: mitte.x + 0.05, z: mitte.z, gieren: 0 });
  // reine Verschiebung: 10 cm nach rechts, 5 cm nach vorn
  let l = S.einhandSchritt(start, { x: mitte.x + 0.15, z: mitte.z - 0.05, gieren: 0 });
  nah(l.x, 0.10); nah(l.z, -0.05); nah(l.drehung, 0); assert.equal(l.massstab, 50);
  // Handdrehung um 90° (gegen den Uhrzeigersinn): das Modell dreht um 90°, die Hand bleibt am selben Punkt des Modells
  const h = { x: mitte.x + 0.05, z: mitte.z, gieren: 90 * GRAD };
  l = S.einhandSchritt(start, h);
  nah(l.drehung, 90 * GRAD);
  // Der Griffpunkt (Hand) liegt vorher 0.05 m neben der Mitte; nachher liegt die Modellmitte um 90° gedreht um die Hand: (−0.05·cos90 + 0, …) → Mitte = Hand + R(90°)(−0.05, 0)
  nah(S.modellMitte(l).x - h.x, 0, 1e-9); nah(S.modellMitte(l).z - h.z, 0.05, 1e-9);
  // ohne Richtung der Hand bleibt die Drehung, es wird nur verschoben
  l = S.einhandSchritt(S.einhandStart(l0, { x: 0, z: -0.9, gieren: null }), { x: 0.1, z: -0.9, gieren: 0.5 });
  nah(l.drehung, 0); nah(l.x, 0.1);
  // das Modell kann nicht vom Tisch geschoben werden
  l = S.einhandSchritt(start, { x: 5, z: 5, gieren: 0 });
  nah(l.x, S.TISCH.breite / 2 - S.TISCH.halt); nah(l.z, S.TISCH.tiefe / 2 - S.TISCH.halt);
});

test('Winkel: Drehung über ±180° ist der kürzeste Weg', () => {
  nah(S.winkelDiff(-170 * GRAD, 170 * GRAD), 20 * GRAD, 1e-9);
  nah(S.winkelDiff(170 * GRAD, -170 * GRAD), -20 * GRAD, 1e-9);
  const l0 = { massstab: 50, drehung: 0, x: 0, z: 0 };
  const m = S.modellMitte(l0);
  const g = S.einhandStart(l0, { x: m.x, z: m.z, gieren: 170 * GRAD });
  nah(S.einhandSchritt(g, { x: m.x, z: m.z, gieren: -170 * GRAD }).drehung, 20 * GRAD, 1e-9);
});

// ------------------------------------------------------------------------------------------------ Zwei Hände: Zoom um den Mittelpunkt
test('Zwei Hände: der Abstand zoomt um den Mittelpunkt, die Stelle unter dem Mittelpunkt bleibt dort', () => {
  const l0 = { massstab: 50, drehung: 0, x: 0.1, z: 0.05 };
  const A = { x: -0.3, z: -0.9 }, B = { x: 0.1, z: -0.9 };           // Abstand 0.4 m, Mitte (−0.1, −0.9)
  const g = S.zweihandStart(l0, A, B);
  const q = (l, p) => { const m = S.modellMitte(l); const d = S.dreheXZ({ x: p.x - m.x, z: p.z - m.z }, -l.drehung); return { x: d.x * l.massstab, z: d.z * l.massstab }; };
  const q0 = q(l0, { x: -0.1, z: -0.9 });
  // Hände doppelt so weit auseinander → Massstab halbiert (1:25), Mittelpunkt zeigt auf dieselbe Stelle des Modells
  const l = S.zweihandSchritt(g, { x: -0.5, z: -0.9 }, { x: 0.3, z: -0.9 });
  nah(l.massstab, 25, 1e-9);
  const q1 = q(l, { x: -0.1, z: -0.9 });
  nah(q1.x, q0.x, 1e-6); nah(q1.z, q0.z, 1e-6);
  // zusammenschieben → kleiner (1:100)
  nah(S.zweihandSchritt(g, { x: -0.2, z: -0.9 }, { x: 0, z: -0.9 }).massstab, 100, 1e-9);
  // Grenzen
  assert.equal(S.zweihandSchritt(g, { x: -2, z: -0.9 }, { x: 2, z: -0.9 }).massstab, 20, 'mindestens 1:20');
  assert.equal(S.zweihandSchritt(g, { x: -0.101, z: -0.9 }, { x: -0.099, z: -0.9 }).massstab, 200, 'höchstens 1:200');
  // Mittelpunkt wandert → das Modell wandert mit (ohne Zoom, ohne Drehung)
  const v = S.zweihandSchritt(g, { x: -0.2, z: -0.8 }, { x: 0.2, z: -0.8 });
  nah(v.massstab, 50, 1e-9); nah(v.x, l0.x + 0.1, 1e-9, 'x'); nah(v.z, l0.z + 0.1, 1e-9, 'z');
});

test('Zwei Hände: die Drehung der Verbindungslinie dreht das Modell um den Mittelpunkt', () => {
  const l0 = { massstab: 50, drehung: 0, x: 0, z: 0 };
  const mitte = { x: 0, z: -0.9 };
  const A = { x: mitte.x - 0.2, z: mitte.z }, B = { x: mitte.x + 0.2, z: mitte.z };
  const g = S.zweihandStart(l0, A, B);
  // Linie um 90° gegen den Uhrzeigersinn gedreht (von +x nach −z): B wandert nach (0, −1.1)
  const l = S.zweihandSchritt(g, { x: mitte.x, z: mitte.z + 0.2 }, { x: mitte.x, z: mitte.z - 0.2 });
  nah(l.drehung, 90 * GRAD, 1e-9); nah(l.massstab, 50, 1e-9);
  // Die Mitte des Modells lag unter dem Mittelpunkt der Hände und bleibt dort
  nah(l.x, 0, 1e-9); nah(l.z, 0, 1e-9);
  // Drehen und Zoomen zugleich
  const l2 = S.zweihandSchritt(g, { x: mitte.x, z: mitte.z + 0.4 }, { x: mitte.x, z: mitte.z - 0.4 });
  nah(l2.drehung, 90 * GRAD, 1e-9); nah(l2.massstab, 25, 1e-9);
});

// ------------------------------------------------------------------------------------------------ Gestenablauf
test('Gesten: ein Pinch am Griff hält ihn, ein Pinch weit weg nicht; bewegt wird nur mit beiden Griffen; der Wechsel springt nicht', () => {
  const l0 = S.einpassen({ b: 12, t: 8, h: 9 });
  const gp = S.griffPunkte(l0, { b: 12, t: 8, h: 9 });
  let g = S.gestenNeu(l0);
  const A = (dx = 0) => ({ griff: true, nah: 0, x: gp[0].x + dx, z: gp[0].z });
  const B = (dx = 0) => ({ griff: true, nah: 1, x: gp[1].x + dx, z: gp[1].z });
  // kein Pinch: Ruhe, auch nah am Griff
  g = S.gestenSchritt(g, { links: { ...A(), griff: false }, rechts: null });
  assert.equal(g.modus, null); assert.deepEqual(g.besitzt, { links: null, rechts: null });
  // Pinch am linken Griff: gehalten, aber das Modell ruht (ein Griff allein)
  g = S.gestenSchritt(g, { links: A(), rechts: null });
  assert.equal(g.modus, 'wartet'); assert.equal(g.besitzt.links, 0);
  const vor = g.lage;
  g = S.gestenSchritt(g, { links: A(0.1), rechts: null });
  assert.deepEqual(g.lage, vor, 'ein Griff allein bewegt nichts');
  // die andere Hand pinch weit weg vom Griff (nah = null): zählt nicht
  g = S.gestenSchritt(g, { links: A(0.1), rechts: { griff: true, nah: null, x: gp[1].x + 1, z: gp[1].z } });
  assert.equal(g.modus, 'wartet'); assert.equal(g.besitzt.rechts, null); assert.deepEqual(g.lage, vor);
  // sie pinchte weit weg schon: ein späteres Hineinwandern in die Fangreichweite fasst nicht (nur die steigende Flanke zählt)
  g = S.gestenSchritt(g, { links: A(0.1), rechts: B() });
  assert.equal(g.modus, 'wartet', 'Pinch war schon gesetzt, bevor die Hand am Griff war');
  // loslassen und am Griff neu pinchen: nun halten beide
  g = S.gestenSchritt(g, { links: A(0.1), rechts: { ...B(), griff: false } });
  g = S.gestenSchritt(g, { links: A(0.1), rechts: B() });
  assert.equal(g.modus, 'zwei'); assert.deepEqual(g.besitzt, { links: 0, rechts: 1 });
  const eins = g.lage;
  g = S.gestenSchritt(g, { links: A(0.1), rechts: B() });
  for (const k of ['massstab', 'drehung', 'x', 'z']) nah(g.lage[k], eins[k], 1e-9, `beim Wechsel springt ${k} nicht`);
  // Hände auseinander: doppelter Abstand → 1:25
  const d0 = gp[1].x - (gp[0].x + 0.1);
  g = S.gestenSchritt(g, { links: { ...A(), x: gp[0].x + 0.1 - d0 / 2 }, rechts: { ...B(), x: gp[1].x + d0 / 2 } });
  nah(g.lage.massstab, l0.massstab / 2, 1e-6, 'Abstand verdoppelt → Massstab halbiert');
  // eine Hand lässt los: das Modell bleibt stehen, die andere bewegt sich ohne Wirkung
  const zoomLage = g.lage;
  g = S.gestenSchritt(g, { links: { ...A(), griff: false }, rechts: B(0.6) });
  assert.equal(g.modus, 'wartet'); assert.deepEqual(g.lage, zoomLage);
  g = S.gestenSchritt(g, { links: null, rechts: null });
  assert.equal(g.modus, null); assert.deepEqual(g.lage, zoomLage); assert.deepEqual(g.besitzt, { links: null, rechts: null });
});

test('Gesten: verschieben und drehen mit beiden Griffen; die Hände dürfen die Griffe vertauschen (rechte Hand am linken Griff)', () => {
  const l0 = S.einpassen({ b: 12, t: 8, h: 9 });
  const G = { b: 12, t: 8, h: 9 };
  const gp = S.griffPunkte(l0, G);
  let g = S.gestenNeu(l0);
  // rechte Hand am linken Griff (0), linke Hand am rechten Griff (1)
  const R = (dx = 0, dz = 0) => ({ griff: true, nah: 0, x: gp[0].x + dx, z: gp[0].z + dz }), L = (dx = 0, dz = 0) => ({ griff: true, nah: 1, x: gp[1].x + dx, z: gp[1].z + dz });
  g = S.gestenSchritt(g, { links: { ...L(), griff: false }, rechts: { ...R(), griff: false } });
  g = S.gestenSchritt(g, { links: L(), rechts: R() });
  assert.equal(g.modus, 'zwei'); assert.deepEqual(g.besitzt, { links: 1, rechts: 0 });
  // beide 10 cm nach vorn (−z) und 5 cm nach rechts: das Modell wandert mit
  g = S.gestenSchritt(g, { links: L(0.05, -0.1), rechts: R(0.05, -0.1) });
  nah(g.lage.x, 0.05, 1e-9); nah(g.lage.z, -0.1, 1e-9); nah(g.lage.drehung, 0, 1e-9); nah(g.lage.massstab, l0.massstab, 1e-9);
  // Drehung: die Verbindungslinie (Griff 0 → Griff 1) um 90° gegen den Uhrzeigersinn gedreht → das Modell dreht um 90°
  const m = { x: (gp[0].x + gp[1].x) / 2, z: (gp[0].z + gp[1].z) / 2 }, halb = Math.abs(gp[1].x - gp[0].x) / 2;
  g = S.gestenSchritt(g, { links: { ...L(), x: m.x, z: m.z - halb }, rechts: { ...R(), x: m.x, z: m.z + halb } });
  nah(g.lage.drehung, 90 * GRAD, 1e-9);
});

test('Gesten: derselbe Griff wird nicht von zwei Händen gehalten', () => {
  const l0 = S.einpassen({ b: 12, t: 8, h: 9 });
  let g = S.gestenNeu(l0);
  const beide = { links: { griff: true, nah: 0, x: 0, z: -1 }, rechts: { griff: true, nah: 0, x: 0.01, z: -1 } };
  g = S.gestenSchritt(g, beide);
  assert.deepEqual(g.besitzt, { links: 0, rechts: null }, 'die linke Hand hält ihn zuerst');
  assert.equal(g.modus, 'wartet');
});

test('Richtung der Hand: aus Handgelenk und Mittelfinger, ersatzweise aus dem Zielstrahl', () => {
  const g = { wrist: { x: 0, y: 1, z: 0 }, 'middle-finger-phalanx-proximal': { x: 0, y: 1, z: -0.09 } };
  nah(S.handGieren({ gelenke: g }), 90 * GRAD, 1e-9);                  // nach −z = Gieren 90°
  nah(S.handGieren({ gelenke: { wrist: g.wrist, 'middle-finger-phalanx-proximal': { x: 0.09, y: 1, z: 0 } } }), 0, 1e-9);
  nah(S.winkelDiff(S.handGieren({ strahl: { r: { x: -1, y: 0, z: 0 } } }), Math.PI), 0, 1e-9);   // ±180° sind dieselbe Richtung
  assert.equal(S.handGieren({ strahl: { r: { x: 0, y: -1, z: 0.01 } } }), null, 'senkrecht: keine Richtung');
  assert.equal(S.handGieren(null), null);
});

// ------------------------------------------------------------------------------------------------ Geschosse und Schnitthöhe
const GESCHOSSE = [
  { kennzeichen: '2.OG', name: '2. Obergeschoss', kote: 5800, hoehe: 2900 },
  { kennzeichen: 'EG', name: 'Erdgeschoss', kote: 0, hoehe: 2900 },
  { kennzeichen: '1.OG', name: '1. Obergeschoss', kote: 2900, hoehe: 2900 },
  { kennzeichen: 'UG', name: 'Untergeschoss', kote: -2900, hoehe: 2900 },
  { kennzeichen: 'XX', name: 'ohne Höhe', kote: 9000 },
];

test('Geschosse: sortiert nach Kote, Geschosse ohne Höhe fehlen, Oberkante der Decke = Kote des Geschosses darüber, sonst Kote + Höhe', () => {
  const g = S.geschosseAufbereiten(GESCHOSSE, []);
  assert.deepEqual(g.map((x) => x.kennzeichen), ['UG', 'EG', '1.OG', '2.OG']);
  assert.deepEqual(g.map((x) => x.deckeOK), [0, 2900, 5800, 8700]);
  assert.ok(g.every((x) => x.ohneDecke && x.deckeUK === x.deckeOK), 'ohne Decken im Modell gilt die Oberkante');
  // liegt das Geschoss darüber höher als Kote + Höhe (Decke dazwischen), gilt weiter die Kote darüber
  const g2 = S.geschosseAufbereiten([{ kennzeichen: 'A', kote: 0, hoehe: 2700 }, { kennzeichen: 'B', kote: 3000, hoehe: 2700 }], []);
  assert.equal(g2[0].deckeOK, 3000); assert.equal(g2[1].deckeOK, 5700);
  // zwei Gebäude: das Geschoss darüber gehört zum selben Gebäude
  const g3 = S.geschosseAufbereiten([{ kennzeichen: 'A-EG', kote: 0, hoehe: 2800, teilVon: 'A' }, { kennzeichen: 'B-1', kote: 1000, hoehe: 2800, teilVon: 'B' }], []);
  assert.equal(g3[0].deckeOK, 2800);
});

test('Schnitthöhe: unter der Decke = Unterkante der Decke − SCHNITT_TIEFER_MM, mit «Decke zeigen» = Oberkante; ohne Decke die Oberkante − SCHNITT_TIEFER_MM', () => {
  const decken = [{ oberkante: 2900, dicke: 200 }, { oberkante: 2901, dicke: 250 }, { oberkante: 5800, dicke: 180 }, { oberkante: 8700, dicke: 0 }, { oberkante: 4000, dicke: 300 }];
  const g = S.geschosseAufbereiten(GESCHOSSE, decken);
  const eg = g.find((x) => x.kennzeichen === 'EG'), og1 = g.find((x) => x.kennzeichen === '1.OG'), og2 = g.find((x) => x.kennzeichen === '2.OG'), ug = g.find((x) => x.kennzeichen === 'UG');
  assert.equal(eg.dicke, 250, 'die dickere der beiden Platten auf der Kote (±2 mm) zählt');
  assert.equal(S.SCHNITT_TIEFER_MM, 50); assert.equal(S.schnittHoeheMm(eg, false), 2600); assert.equal(S.schnittHoeheMm(eg, true), 2900);
  assert.equal(S.schnittHoeheMm(og1, false), 5570); assert.equal(S.schnittHoeheMm(og1, true), 5800);
  assert.equal(og2.ohneDecke, true, 'Dicke 0 zählt nicht'); assert.equal(S.schnittHoeheMm(og2, false), 8650);
  assert.equal(ug.ohneDecke, true); assert.equal(S.schnittHoeheMm(ug, false), -50);
});

test('Clipping-Ebene im Raum: Plattenoberkante + (Schnitt − Unterkante des Modells) / Massstab, Spiel nach der Seite der Fläche', () => {
  const unten = -2.9;   // Unterkante des Modells (m), UG-Boden
  const p = { schnittMm: 2650, deckeZeigen: false, modellUnten: unten, massstab: 50 };
  nah(S.schnittEbeneY(p), S.TISCH.hoehe + ((2650 - S.SPIEL_MM) / 1000 + 2.9) / 50, 1e-12);
  nah(S.schnittEbeneY({ ...p, schnittMm: 2900, deckeZeigen: true }), S.TISCH.hoehe + ((2900 + S.SPIEL_MM) / 1000 + 2.9) / 50, 1e-12);
  // beim Zoomen wandert die Ebene mit dem Modell: bei 1:25 doppelt so hoch über der Platte
  const a = S.schnittEbeneY(p) - S.TISCH.hoehe, b = S.schnittEbeneY({ ...p, massstab: 25 }) - S.TISCH.hoehe;
  nah(b / a, 2, 1e-9);
  // Unterkante des Modells auf der Platte: Schnitt in Höhe der Unterkante liegt auf der Platte
  nah(S.schnittEbeneY({ schnittMm: 0, deckeZeigen: false, modellUnten: 0, massstab: 50 }) - S.TISCH.hoehe, -S.SPIEL_MM / 1000 / 50, 1e-12);
});

test('«Alle Etagen» und Zurücksetzen: Kopfzeilen, Umschalter, Startlage', () => {
  const g = S.geschosseAufbereiten(GESCHOSSE, [{ oberkante: 2900, dicke: 200 }]);
  const lage = { massstab: 50, drehung: 0, x: 0, z: 0 };
  assert.deepEqual(S.kopfZeilen(lage, { geschoss: null, deckeZeigen: false }, g), ['Sitzungsraum · Massstab 1:50', 'Alle Etagen']);
  const z = S.kopfZeilen({ ...lage, massstab: 25 }, { geschoss: 'EG', deckeZeigen: false }, g);
  assert.equal(z[0], 'Sitzungsraum · Massstab 1:25'); assert.ok(z[1].startsWith('EG · Schnitt unter der Decke (2.65 m)'), z[1]);
  assert.ok(S.kopfZeilen(lage, { geschoss: 'EG', deckeZeigen: true }, g)[1].includes('über der Decke (2.90 m)'));
  assert.ok(S.kopfZeilen(lage, { geschoss: '1.OG', deckeZeigen: false }, g).some((t) => t.startsWith('Keine Decke im Modell')));
  assert.deepEqual(S.sitzungsTasten({ geschoss: null, deckeZeigen: false }, false), { alle: true, decke: false, ifc: false });
  assert.deepEqual(S.sitzungsTasten({ geschoss: 'EG', deckeZeigen: true }, true), { alle: false, decke: true, ifc: true, 'g:EG': true });
  // Zurücksetzen: Startlage neu eingepasst, auch nach Zoom und Drehung
  const gross = { b: 100, t: 30, h: 20 };
  assert.deepEqual(S.zuruecksetzen(gross), S.einpassen(gross));
  assert.equal(S.zuruecksetzen({ b: 12, t: 8, h: 9 }).massstab, 50);
});

// ------------------------------------------------------------------------------------------------ Tablet-Seite
test('Tablet-Seite: Alle Etagen und die Geschosse (oberstes zuerst), feste Tasten, jede Taste trifft sich selbst, nichts überlappt', async () => {
  const T = await import('./xr-greifen-logik.js');
  const g = S.geschosseAufbereiten(GESCHOSSE, []);
  const layout = S.sitzungsLayout(g);
  assert.deepEqual(layout.slice(0, 5).map((t) => t.id), ['alle', 'g:2.OG', 'g:1.OG', 'g:EG', 'g:UG']);
  for (const id of ['decke', 'zuruecksetzen', 'ifc', 'raum', 'ende']) assert.ok(layout.find((t) => t.id === id), id);
  assert.equal(layout.find((t) => t.id === 'raum').text, 'Begehen');
  for (const t of layout) {
    assert.equal(T.tasteBei((t.x + t.w / 2) / T.TABLET_PX.b, (t.y + t.h / 2) / T.TABLET_PX.h, layout)?.id, t.id, t.id);
    assert.ok(t.x >= 0 && t.y >= T.TABLET_KOPF_H && t.x + t.w <= T.TABLET_PX.b && t.y + t.h <= T.TABLET_PX.h, `${t.id} im Bild`);
    assert.ok(t.h >= 30, `${t.id} hoch genug zum Antippen (${t.h} px)`);
  }
  for (let i = 0; i < layout.length; i++) for (let j = i + 1; j < layout.length; j++) {
    const a = layout[i], b = layout[j];
    assert.ok(a.x + a.w <= b.x + 1e-9 || b.x + b.w <= a.x + 1e-9 || a.y + a.h <= b.y + 1e-9 || b.y + b.h <= a.y + 1e-9, `${a.id} überlappt ${b.id}`);
  }
  // viele Geschosse: höchstens MAX_ETAGEN, das Layout bleibt im Bild
  const viele = S.geschosseAufbereiten(Array.from({ length: 14 }, (_, i) => ({ kennzeichen: `G${i}`, kote: i * 3000, hoehe: 3000 })), []);
  const l2 = S.sitzungsLayout(viele);
  assert.equal(l2.filter((t) => t.id.startsWith('g:')).length, S.MAX_ETAGEN);
  assert.ok(l2.every((t) => t.y + t.h <= T.TABLET_PX.h && t.h >= 30), 'alle Tasten im Bild und gross genug');
  // ohne Geschosse nur «Alle Etagen»
  assert.deepEqual(S.sitzungsLayout([]).slice(0, 1).map((t) => t.id), ['alle']);
  // Beschriftung kurz
  assert.equal(S.geschossText({ kennzeichen: 'EG' }), 'EG');
  assert.ok(S.geschossText({ kennzeichen: 'GESCHOSS-WEIT-LANG', name: 'Dachgeschoss mit Galerie' }).length <= S.GESCHOSS_TEXT_MAX);
});

test('Tablet-Seite im Hochformat: Etagen untereinander in voller Breite, darunter zwei Spalten, «VR beenden» ganz unten, Texte ungekürzt', async () => {
  const T = await import('./xr-greifen-logik.js');
  const g = S.geschosseAufbereiten(GESCHOSSE, []);
  const layout = S.sitzungsLayout(g);
  const etagen = layout.filter((t) => t.id === 'alle' || t.id.startsWith('g:'));
  assert.equal(etagen.length, 5);
  assert.ok(etagen.every((t) => t.x === etagen[0].x && t.w === etagen[0].w && t.w > T.TABLET_PX.b * 0.9), 'volle Breite, untereinander');
  assert.ok(etagen.every((t, i) => i === 0 || t.y > etagen[i - 1].y), 'eine Zeile je Etage');
  const unten = ['decke', 'zuruecksetzen', 'ifc', 'raum'].map((id) => layout.find((t) => t.id === id));
  assert.equal(unten[0].y, unten[1].y); assert.equal(unten[2].y, unten[3].y); assert.ok(unten[2].y > unten[0].y); assert.ok(unten[0].x < unten[1].x);
  assert.ok(unten[0].y > etagen[etagen.length - 1].y, 'die Tasten stehen unter der Etagenliste');
  assert.ok(layout.find((t) => t.id === 'ende').y > unten[2].y);
  // 15 Zoll hochkant
  assert.ok(T.TABLET_H > T.TABLET_B && T.TABLET_PX.h > T.TABLET_PX.b, 'Hochformat');
  // Begehen-Seite: zwei Spalten, alle zwölf Tasten, keine Beschriftung gekürzt
  const b = T.tabletLayout();
  assert.equal(b.length, 13); assert.equal(T.TABLET_SPALTEN, 2);
  assert.ok(b.filter((t) => t.id !== 'ende').every((t) => t.x < T.TABLET_PX.b / 2 ? t.w < T.TABLET_PX.b / 2 : t.x > T.TABLET_PX.b / 2 - 1), 'zwei Spalten');
  assert.ok(b.every((t) => t.h >= 40 && t.y + t.h <= T.TABLET_PX.h && t.x >= 0 && t.x + t.w <= T.TABLET_PX.b), 'gross genug und im Bild');
  for (const t of b) assert.equal(T.tasteBei((t.x + t.w / 2) / T.TABLET_PX.b, (t.y + t.h / 2) / T.TABLET_PX.h, b)?.id, t.id, t.id);
  for (const t of T.TABLET_TASTEN) assert.ok(t.text.length > 0 && t.text.length <= 20, t.text);
});

// ------------------------------------------------------------------------------------------------ Ausschnitt («Raum zeigen (VR)», 3D-Ansicht in VR)
const BOX = { aktiv: true, min: [0, 0, 0], max: [10000, 8000, 3000], winkel: 0 };

test('Ausschnitt: Grösse, Mitte und Unterkante aus dem Quader (Koordinaten der 3D-Ansicht: z = −Nord), ungültig oder ausgeschaltet gibt null', () => {
  const b = S.ausschnittBezug(BOX);
  nah(b.groesse.b, 10); nah(b.groesse.t, 8); nah(b.groesse.h, 3);
  nah(b.anker.x, 5); nah(b.anker.z, -4); nah(b.unten, 0);
  const hoch = S.ausschnittBezug({ ...BOX, min: [1000, 2000, 2900], max: [3000, 6000, 5800] });
  nah(hoch.anker.x, 2); nah(hoch.anker.z, -4); nah(hoch.unten, 2.9); nah(hoch.groesse.h, 2.9);
  assert.equal(S.ausschnittBezug({ ...BOX, aktiv: false }), null);
  assert.equal(S.ausschnittBezug(null), null);
  assert.equal(S.ausschnittBezug({ min: [0, 0, 0], max: [0.5, 1, 1] }), null, 'kleiner als 1 mm');
  assert.equal(S.ausschnittBezug({ aktiv: true, min: [0, 0], max: [1, 1] }), null);
  // gedrehter Quader: die Achsenbox ist grösser als die Kantenlängen
  const g = S.ausschnittBezug({ ...BOX, winkel: 30 });
  assert.ok(g.groesse.b > 10 && g.groesse.t > 8);
});

test('Ausschnitt: der Massstab passt den Ausschnitt auf die Platte – so fein wie möglich (1:20), gröber nur wenn nötig, höchstens 1:200', () => {
  assert.equal(S.ausschnittLage({ b: 10, t: 8, h: 3 }).massstab, 20, 'Raum plus 1.5 m: 1:20 genügt');
  assert.equal(S.ausschnittLage({ b: 40, t: 30, h: 9 }).massstab, 38, 'gross: nächster ganzer Massstab, der noch auf die Platte (1.4 × 0.8 m) passt');
  assert.ok(40 / 38 <= 1.4 + 1e-9 && 30 / 38 <= 0.8 + 1e-9);
  assert.equal(S.ausschnittLage({ b: 400, t: 300, h: 9 }).massstab, 200);
  assert.equal(S.ausschnittLage({ b: 400, t: 300, h: 9 }).passt, false);
  assert.equal(S.ausschnittLage({ b: 1, t: 1, h: 1 }).massstab, 20, 'ein kleiner Ausschnitt wird nicht über 1:20 hinaus vergrössert');
  assert.equal(S.einpassen({ b: 10, t: 8, h: 3 }).massstab, 50, 'zum Vergleich: das ganze Modell beginnt bei 1:50');
  const z = S.ausschnittLage({ b: 10, t: 8, h: 3 });
  assert.ok(z.x === 0 && z.z === 0 && z.drehung === 0, 'Mitte der Platte, nicht gedreht');
});

test('Ausschnitt: sechs Ebenen, die genau den Quader sichtbar lassen', () => {
  const e = S.ausschnittEbenen(BOX);
  assert.equal(e.length, 6);
  const abstand = (p) => e.map((x) => x.normale[0] * p.x + x.normale[1] * p.y + x.normale[2] * p.z + x.konstante);
  assert.ok(abstand({ x: 5, y: 1.5, z: -4 }).every((d) => d > 0), 'Mitte: innen');
  assert.ok(abstand({ x: 0, y: 0, z: 0 }).every((d) => d > -1e-9), 'Ecke liegt auf den Ebenen');
  for (const aus of [{ x: -0.01, y: 1, z: -4 }, { x: 10.01, y: 1, z: -4 }, { x: 5, y: -0.01, z: -4 }, { x: 5, y: 3.01, z: -4 }, { x: 5, y: 1, z: 0.01 }, { x: 5, y: 1, z: -8.01 }])
    assert.ok(abstand(aus).some((d) => d < 0), `ausserhalb: ${JSON.stringify(aus)}`);
  // gedrehter Quader (30°): die Ebenen drehen mit
  const r = S.ausschnittEbenen({ ...BOX, winkel: 30 });
  assert.equal(r.length, 6);
  assert.deepEqual(S.ausschnittEbenen(null), []);
});

test('Ausschnitt: Kopfzeilen und Tasten – «Alle Etagen» ist aus, solange der Ausschnitt gilt', () => {
  const lage = { massstab: 20, drehung: 0, x: 0, z: 0 };
  const z = S.kopfZeilen(lage, { geschoss: null, deckeZeigen: false, ausschnitt: 'Raum E.01 + 1.5 m' }, []);
  assert.equal(z[0], 'Sitzungsraum · Massstab 1:20'); assert.equal(z[1], 'Ausschnitt: Raum E.01 + 1.5 m'); assert.ok(z[2].includes('Alle Etagen')); assert.equal(z.length, 3);
  assert.deepEqual(S.sitzungsTasten({ geschoss: null, deckeZeigen: false, ausschnitt: 'x' }, false), { alle: false, decke: false, ifc: false });
  assert.equal(S.sitzungsTasten({ geschoss: null, deckeZeigen: false, ausschnitt: null }, false).alle, true);
});
