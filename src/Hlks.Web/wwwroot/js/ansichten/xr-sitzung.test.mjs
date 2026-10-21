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

// ------------------------------------------------------------------------------------------------ Nähe
test('Nähe: nur wer beim Zufassen am Modell ist, greift', () => {
  const l = S.einpassen({ b: 12, t: 8, h: 9 });
  const g = { b: 12, t: 8, h: 9 };
  const m = S.modellMitte(l);
  assert.equal(S.nahAmModell({ x: m.x, y: S.TISCH.hoehe + 0.1, z: m.z }, l, g), true);
  assert.equal(S.nahAmModell({ x: m.x + 0.3, y: S.TISCH.hoehe + 0.1, z: m.z }, l, g), true, 'Umkreis 0.5 m·√(12²+8²)/50 ≈ 0.14 plus 0.25 m');
  assert.equal(S.nahAmModell({ x: m.x + 0.7, y: S.TISCH.hoehe + 0.1, z: m.z }, l, g), false, 'weit daneben');
  assert.equal(S.nahAmModell({ x: m.x, y: S.TISCH.hoehe + 1.2, z: m.z }, l, g), false, 'viel zu hoch');
  assert.equal(S.nahAmModell({ x: m.x, y: 0.2, z: m.z }, l, g), false, 'unter dem Tisch');
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
test('Gesten: Zufassen in der Nähe, Zufassen ausserhalb zählt nicht, der Wechsel von einer auf zwei Hände springt nicht', () => {
  const l0 = S.einpassen({ b: 12, t: 8, h: 9 });
  const m = S.modellMitte(l0);
  let g = S.gestenNeu(l0);
  const aus = { griff: false, nah: true, x: m.x, z: m.z, gieren: 0 };
  // eine Hand greift in der Nähe
  g = S.gestenSchritt(g, { links: null, rechts: aus });
  assert.equal(g.modus, null);
  g = S.gestenSchritt(g, { links: null, rechts: { ...aus, griff: true } });
  assert.equal(g.modus, 'ein');
  const vor = g.lage;
  g = S.gestenSchritt(g, { links: null, rechts: { ...aus, griff: true, x: m.x + 0.1 } });
  nah(g.lage.x - vor.x, 0.1, 1e-9, 'folgt der Hand');
  // zweite Hand greift ausserhalb der Nähe von aussen kommend: zählt nicht (kein Zufassen am Modell)
  g = S.gestenSchritt(g, { links: { griff: true, nah: false, x: m.x - 1.2, z: m.z, gieren: 0 }, rechts: { ...aus, griff: true, x: m.x + 0.1 } });
  assert.equal(g.modus, 'ein');
  g = S.gestenSchritt(g, { links: { griff: false, nah: false, x: m.x - 1.2, z: m.z, gieren: 0 }, rechts: { ...aus, griff: true, x: m.x + 0.1 } });
  // nun greift die linke Hand nah am Modell zu: zwei Hände, Massstab und Lage ändern sich im selben Schritt noch nicht
  const links = { griff: true, nah: true, x: m.x - 0.2, z: m.z, gieren: 0 }, rechts = { griff: true, nah: true, x: m.x + 0.2, z: m.z, gieren: 0 };
  const nachher = S.gestenSchritt(g, { links: { ...links, griff: false }, rechts });   // Flanke erst mit griff = true
  g = nachher;
  const eins = g.lage;
  g = S.gestenSchritt(g, { links, rechts });
  assert.equal(g.modus, 'zwei');
  assert.deepEqual(g.lage, eins, 'beim Wechsel springt nichts');
  g = S.gestenSchritt(g, { links: { ...links, x: m.x - 0.4 }, rechts: { ...rechts, x: m.x + 0.4 } });
  nah(g.lage.massstab, 25, 1e-9, 'doppelter Abstand');
  // eine Hand lässt los: weiter mit einer, wieder ohne Sprung
  const zoomLage = g.lage;
  g = S.gestenSchritt(g, { links: { ...links, griff: false }, rechts: { ...rechts, x: m.x + 0.4 } });
  assert.equal(g.modus, 'ein'); assert.deepEqual(g.lage, zoomLage);
  // beide los: Ruhe, die Lage bleibt
  g = S.gestenSchritt(g, { links: null, rechts: null });
  assert.equal(g.modus, null); assert.deepEqual(g.lage, zoomLage);
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

test('Schnitthöhe: unter der Decke = Unterkante der Decke, mit «Decke zeigen» = Oberkante; ohne Decke die Oberkante', () => {
  const decken = [{ oberkante: 2900, dicke: 200 }, { oberkante: 2901, dicke: 250 }, { oberkante: 5800, dicke: 180 }, { oberkante: 8700, dicke: 0 }, { oberkante: 4000, dicke: 300 }];
  const g = S.geschosseAufbereiten(GESCHOSSE, decken);
  const eg = g.find((x) => x.kennzeichen === 'EG'), og1 = g.find((x) => x.kennzeichen === '1.OG'), og2 = g.find((x) => x.kennzeichen === '2.OG'), ug = g.find((x) => x.kennzeichen === 'UG');
  assert.equal(eg.dicke, 250, 'die dickere der beiden Platten auf der Kote (±2 mm) zählt');
  assert.equal(S.schnittHoeheMm(eg, false), 2650); assert.equal(S.schnittHoeheMm(eg, true), 2900);
  assert.equal(S.schnittHoeheMm(og1, false), 5620); assert.equal(S.schnittHoeheMm(og1, true), 5800);
  assert.equal(og2.ohneDecke, true, 'Dicke 0 zählt nicht'); assert.equal(S.schnittHoeheMm(og2, false), 8700);
  assert.equal(ug.ohneDecke, true); assert.equal(S.schnittHoeheMm(ug, false), 0);
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
  assert.equal(z[0], 'Sitzungsraum · Massstab 1:25'); assert.ok(z[1].startsWith('EG · Schnitt unter der Decke (2.70 m)'), z[1]);
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
  assert.ok(S.geschossText({ kennzeichen: 'GESCHOSS-WEIT-LANG', name: 'Dachgeschoss mit Galerie' }).length <= 14);
});
