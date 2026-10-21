// Tests von «Raum begehen» (N15) – der Teil ohne Browser: begehen-logik.js (Bewegungsrechnung, Tastenbelegung, Tastenverlauf, Einblendung).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/begehen.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  AUGENHOEHE_VORGABE, AUGENHOEHE_MIN, AUGENHOEHE_MAX, AUGENHOEHE_SCHLUESSEL, FOV_GRAD, SCHRITT_MM, GESCHWINDIGKEIT, DREH_SCHRITT_GRAD, DREH_GESCHWINDIGKEIT,
  SCHNELL, HOEHE_SCHRITT_MM, NICKEN_MAX_GRAD, HALTEN_MS, TASTEN, TASTENHILFE, FLUESSIG, tastenAktion, augenhoeheLesen, zustandAusStandort, blickVektor,
  vorwaerts, rechtsVektor, kameraWerte, projektWerte, gehen, seitwaerts, drehen, umsehen, augenhoeheAendern, verschieben, tippen, fliessend, tastenVerlauf, einblendung,
} from './begehen-logik.js';
import { gebaeudeAusrichtungGrad } from './wuerfel-logik.js';

const GRAD = Math.PI / 180;
const nah = (a, b, eps = 1e-9, t = '') => assert.ok(Math.abs(a - b) < eps, `${t} ${a} ≠ ${b}`);

/** Antwort der Abfrage raum_begehen (Kern): Standpunkt in Projekt-mm, Blick Azimut gegen den Uhrzeigersinn ab +x (90° = Nord). */
const standort = (azimut = 90, extra = {}) => ({
  raum: 'R-1', name: 'Wohnen', nummer: '0.01', geschoss: 'EG', geschossName: 'Erdgeschoss',
  standpunkt: { x: 2000, y: 1500, z: 1700, art: 'schwerpunkt', abstandZumRand: 1400 }, bodenOk: 0, augenhoehe: 1700,
  blick: { azimutGrad: azimut, richtung: [Math.cos(azimut * GRAD), Math.sin(azimut * GRAD), 0], nickenGrad: 0, planwinkel: 0 },
  ...extra,
});

// ------------------------------------------------------------------------------------------------ Standort → Zustand

test('Zustand aus dem Standort: Projekt-mm → three-Meter (x = Ost, z = −Nord), Augen 1.7 m über Boden', () => {
  const z = zustandAusStandort(standort());
  nah(z.x, 2); nah(z.z, -1.5); nah(z.boden, 0); nah(z.augen, 1.7); nah(z.gieren, Math.PI / 2); nah(z.nicken, 0);
  const k = kameraWerte(z);
  assert.deepEqual([k.position.x, k.position.y, k.position.z].map((v) => Math.round(v * 1e6) / 1e6), [2, 1.7, -1.5]);
  // Blick nach Projekt-Nord = three −z, waagrecht
  nah(k.ziel.x, 2, 1e-9); nah(k.ziel.y, 1.7, 1e-9); nah(k.ziel.z, -2.5, 1e-9);
});

test('Obergeschoss: Augenhöhe zählt ab OK Boden des Raums', () => {
  const z = zustandAusStandort(standort(90, { bodenOk: 3000 }));
  nah(kameraWerte(z).position.y, 4.7);
  const p = projektWerte(z);
  assert.equal(p.z, 4700); assert.equal(p.bodenOk, 3000); assert.equal(p.augenhoehe, 1700);
});

test('Augenhöhe der Benutzereinstellung übersteuert die der Abfrage, ungültige Werte werden begrenzt bzw. ersetzt', () => {
  nah(zustandAusStandort(standort(), 1200).augen, 1.2);
  nah(zustandAusStandort(standort(), 99999).augen, AUGENHOEHE_MAX / 1000);
  assert.equal(augenhoeheLesen(null), AUGENHOEHE_VORGABE);
  assert.equal(augenhoeheLesen(''), AUGENHOEHE_VORGABE);
  assert.equal(augenhoeheLesen('abc'), AUGENHOEHE_VORGABE);
  assert.equal(augenhoeheLesen(50), AUGENHOEHE_VORGABE);
  assert.equal(augenhoeheLesen(9999), AUGENHOEHE_VORGABE);
  assert.equal(augenhoeheLesen(1500), 1500);
  assert.equal(augenhoeheLesen('1650'), 1650);
  assert.equal(AUGENHOEHE_VORGABE, 1700); assert.equal(AUGENHOEHE_MIN, 300); assert.equal(AUGENHOEHE_SCHLUESSEL, 'ansicht3d.augenhoehe'); assert.equal(FOV_GRAD, 75);
});

test('unvollständiger Standort gibt null', () => {
  assert.equal(zustandAusStandort(null), null);
  assert.equal(zustandAusStandort({ standpunkt: { x: 1, y: 2 } }), null);
  assert.equal(zustandAusStandort({ ...standort(), blick: {} }), null);
});

test('Blick mit Planwinkel ≠ 0: der Azimut des Kerns (90° − Planwinkel) ergibt dieselbe Richtung wie «VORNE» des Navigationswürfels', () => {
  // Gebäude um 37° gedreht → planwinkel −37 → Würfel-Ausrichtung θ = +37°; Kern: Azimut 90 + 37 = 127°.
  const theta = gebaeudeAusrichtungGrad({ planwinkel: -37 });
  assert.equal(theta, 37);
  const z = zustandAusStandort(standort(127));
  const b = blickVektor(z);
  // Würfel: die Kamera sitzt auf VORNE = (0,0,1) um θ gedreht (+x → −z): Kamera-Richtung vom Ziel = (sin θ·(−1)… ) – hier die Blickrichtung (Gegenteil)
  // (0,0,−1) um θ gegen den Uhrzeigersinn von oben gedreht: (−sin θ, 0, −cos θ)
  nah(b.x, -Math.sin(theta * GRAD), 1e-9, 'x'); nah(b.y, 0, 1e-12, 'y'); nah(b.z, -Math.cos(theta * GRAD), 1e-9, 'z');
});

// ------------------------------------------------------------------------------------------------ Bewegung

test('Pfeil hoch: ein Schritt von 100 mm in Blickrichtung, die Höhe bleibt', () => {
  const z0 = zustandAusStandort(standort(90));
  const z1 = tippen(z0, 'vor');
  nah(z1.x, 2); nah(z1.z, -1.5 - 0.1);                // nach Nord = −z
  nah(kameraWerte(z1).position.y, kameraWerte(z0).position.y);
  const z2 = tippen(z1, 'zurueck');
  nah(z2.x, z0.x); nah(z2.z, z0.z);
  assert.equal(SCHRITT_MM, 100);
});

test('Gehen in jede Richtung: bei Azimut 0 (Ost) wandert x, bei 180 (West) −x, bei 127° gemäss Cosinus/Sinus', () => {
  const ost = gehen(zustandAusStandort(standort(0)), 1);
  nah(ost.x, 3); nah(ost.z, -1.5);
  const west = gehen(zustandAusStandort(standort(180)), 1);
  nah(west.x, 1); nah(west.z, -1.5, 1e-9);
  const s = gehen(zustandAusStandort(standort(127)), 2);
  nah(s.x, 2 + 2 * Math.cos(127 * GRAD)); nah(s.z, -1.5 - 2 * Math.sin(127 * GRAD));
});

test('Höhe über Boden bleibt beim Gehen gleich, auch mit Nicken und über viele Schritte', () => {
  let z = umsehen(zustandAusStandort(standort(33)), 0, 200);   // steil nach unten schauen
  const y0 = kameraWerte(z).position.y;
  for (let i = 0; i < 50; i++) z = tippen(z, i % 3 === 0 ? 'zurueck' : 'vor');
  for (let i = 0; i < 20; i++) z = tippen(z, 'links');
  z = fliessend(z, new Set(['vor']), 2, true);
  nah(kameraWerte(z).position.y, y0, 1e-12);
});

test('Pfeil links/rechts dreht um 5°; links = gegen den Uhrzeigersinn (Blick geht von Nord nach West)', () => {
  const z0 = zustandAusStandort(standort(90));
  const l = tippen(z0, 'links');
  nah(l.gieren, (90 + DREH_SCHRITT_GRAD) * GRAD);
  const b = blickVektor(l);
  assert.ok(b.x < 0, 'der Blick dreht nach West (−x)');
  const r = tippen(z0, 'rechts');
  nah(r.gieren, (90 - DREH_SCHRITT_GRAD) * GRAD);
  assert.ok(blickVektor(r).x > 0, 'der Blick dreht nach Ost (+x)');
  // das Drehen ändert den Standpunkt nicht
  nah(l.x, z0.x); nah(l.z, z0.z);
  // vier Vierteldrehungen = ganze Drehung
  let z = z0;
  for (let i = 0; i < 72; i++) z = tippen(z, 'links');
  nah(Math.cos(z.gieren), Math.cos(z0.gieren), 1e-9); nah(Math.sin(z.gieren), Math.sin(z0.gieren), 1e-9);
});

test('Umschalt = dreifach schneller (Schritt, Drehung, Höhe und flüssig)', () => {
  const z0 = zustandAusStandort(standort(90));
  nah(z0.z - tippen(z0, 'vor', true).z, 3 * 0.1);
  nah(tippen(z0, 'links', true).gieren - z0.gieren, 3 * DREH_SCHRITT_GRAD * GRAD);
  nah(tippen(z0, 'hoeher', true).augen - z0.augen, 0.3);
  const a = fliessend(z0, new Set(['vor']), 1, false), b = fliessend(z0, new Set(['vor']), 1, true);
  nah(z0.z - a.z, GESCHWINDIGKEIT); nah(z0.z - b.z, GESCHWINDIGKEIT * SCHNELL);
  nah(fliessend(z0, new Set(['links']), 1).gieren - z0.gieren, DREH_GESCHWINDIGKEIT * GRAD);
  assert.equal(SCHNELL, 3);
});

test('Flüssig: 1.4 m/s und 90°/s; Vor und Zurück zugleich heben sich auf', () => {
  const z0 = zustandAusStandort(standort(90));
  nah(z0.z - fliessend(z0, new Set(['vor']), 0.5).z, 0.7);
  const nichts = fliessend(z0, new Set(['vor', 'zurueck', 'links', 'rechts']), 1);
  nah(nichts.z, z0.z); nah(nichts.gieren, z0.gieren);
  assert.equal(fliessend(z0, new Set(), 1), z0, 'ohne gehaltene Taste bleibt der Zustand derselbe');
});

test('Umsehen mit der Maus: nach rechts ziehen = nach rechts schauen, nach oben ziehen = nach oben; Nicken auf ±80° begrenzt', () => {
  const z0 = zustandAusStandort(standort(90));
  const re = umsehen(z0, 40, 0);
  nah(re.gieren, z0.gieren - 40 * 0.25 * GRAD);
  assert.ok(blickVektor(re).x > 0, 'Blick geht nach Ost');
  const hoch = umsehen(z0, 0, -40);
  nah(hoch.nicken, 10 * GRAD);
  assert.ok(blickVektor(hoch).y > 0);
  nah(umsehen(z0, 0, -100000).nicken, NICKEN_MAX_GRAD * GRAD, 1e-12);
  nah(umsehen(z0, 0, 100000).nicken, -NICKEN_MAX_GRAD * GRAD, 1e-12);
  // Nicken ändert den Standpunkt nicht
  const n = umsehen(z0, 0, -100000);
  nah(n.x, z0.x); nah(n.z, z0.z); nah(n.boden, z0.boden); nah(n.augen, z0.augen);
  // geradeaus gehen bleibt waagrecht, auch bei Blick nach oben
  const g = gehen(n, 1);
  nah(kameraWerte(g).position.y, kameraWerte(z0).position.y);
});

test('Bild auf/ab ändert die Augenhöhe um 100 mm, begrenzt auf 300 … 5000 mm', () => {
  const z0 = zustandAusStandort(standort());
  nah(tippen(z0, 'hoeher').augen, 1.8); nah(tippen(z0, 'tiefer').augen, 1.6);
  let z = z0;
  for (let i = 0; i < 100; i++) z = tippen(z, 'tiefer');
  nah(z.augen, AUGENHOEHE_MIN / 1000);
  for (let i = 0; i < 100; i++) z = tippen(z, 'hoeher');
  nah(z.augen, AUGENHOEHE_MAX / 1000);
  assert.equal(HOEHE_SCHRITT_MM, 100);
  nah(augenhoeheAendern(z0, -5000).augen, 0.3);
});

test('Mittlere Taste ziehen: seitlich (Greifen) und hoch/runter; Höhe begrenzt', () => {
  const z0 = zustandAusStandort(standort(90));   // Blick nach Nord, rechts = Ost (+x)
  assert.deepEqual([rechtsVektor(z0).x, rechtsVektor(z0).z].map((v) => Math.round(v * 1e9) / 1e9), [1, 0]);
  const rechts = verschieben(z0, -100, 0);          // nach links ziehen → Kamera nach rechts (Welt folgt der Maus)
  nah(rechts.x, z0.x + 0.5); nah(rechts.z, z0.z);
  const links = verschieben(z0, 100, 0);
  nah(links.x, z0.x - 0.5);
  const hoeher = verschieben(z0, 0, 20);            // nach unten ziehen → Kamera hoch
  nah(hoeher.augen, z0.augen + 0.1);
  nah(verschieben(z0, 0, -100000).augen, AUGENHOEHE_MIN / 1000);
  nah(seitwaerts(z0, 1).x, 3);
  assert.equal(drehen(z0, 90).gieren, z0.gieren + Math.PI / 2);
});

// ------------------------------------------------------------------------------------------------ Tastenbelegung und -verlauf

test('Tastenbelegung', () => {
  assert.deepEqual({ ...TASTEN }, {
    ArrowUp: 'vor', ArrowDown: 'zurueck', ArrowLeft: 'links', ArrowRight: 'rechts', PageUp: 'hoeher', PageDown: 'tiefer', Escape: 'ende',
  });
  assert.equal(tastenAktion('ArrowUp'), 'vor');
  assert.equal(tastenAktion('KeyW'), null);
  assert.equal(tastenAktion('Space'), null);
  assert.deepEqual([...FLUESSIG].sort(), ['links', 'rechts', 'vor', 'zurueck']);
  // die Tastenhilfe nennt jede Belegung
  const hilfe = TASTENHILFE.map(([t, w]) => `${t} ${w}`).join(' | ');
  for (const wort of ['vor', 'drehen', 'Umschalt', 'Augenhöhe', 'umsehen', 'verschieben', 'Esc']) assert.ok(hilfe.includes(wort), wort);
});

test('Tastenverlauf: erster Druck = ein Schritt, nach 250 ms flüssig, Loslassen stoppt; Wiederholung des Systems gibt bei Gehen/Drehen keinen Extraschritt', () => {
  const t = tastenVerlauf();
  assert.equal(t.haelt, false);
  assert.equal(t.unten('ArrowUp', 1000, false), 'vor');
  assert.equal(t.haelt, true);
  assert.equal(t.unten('ArrowUp', 1030, true), null, 'Wiederholung des Systems');
  assert.equal(t.unten('ArrowUp', 1030, false), null, 'schon gehalten');
  assert.equal(t.fliessend(1000 + HALTEN_MS - 1).size, 0, 'noch im Einzelschritt');
  assert.deepEqual([...t.fliessend(1000 + HALTEN_MS)], ['vor']);
  assert.equal(t.unten('ArrowLeft', 1400, false), 'links');
  assert.deepEqual([...t.fliessend(1500)].sort(), ['vor']);
  assert.deepEqual([...t.fliessend(1700)].sort(), ['links', 'vor']);
  t.oben('ArrowUp');
  assert.deepEqual([...t.fliessend(2000)], ['links']);
  t.oben('ArrowLeft');
  assert.equal(t.haelt, false);
  // Bild auf/ab: jeder Druck, auch die Wiederholung des Systems, ändert die Höhe; nichts davon läuft flüssig
  assert.equal(t.unten('PageUp', 3000, false), 'hoeher');
  assert.equal(t.unten('PageUp', 3030, true), 'hoeher');
  assert.equal(t.haelt, false);
  assert.equal(t.unten('Escape', 3100, false), null, 'Esc beendet, das macht der Aufrufer');
  assert.equal(t.unten('KeyX', 3100, false), null);
  t.unten('ArrowDown', 4000); t.alleLos();
  assert.equal(t.haelt, false);
});

test('Ein kurzer Tastendruck ist genau ein Schritt von 100 mm; Halten über 1 s legt etwa 1.4 m − Anlaufzeit zurück', () => {
  const t = tastenVerlauf();
  let z = zustandAusStandort(standort(90));
  const z0 = z;
  const s = t.unten('ArrowUp', 0);
  z = tippen(z, s);
  nah(z0.z - z.z, 0.1, 1e-9);                       // tippen
  // 20 Bilder à 50 ms bis 1000 ms
  let letzte = 0;
  for (let ms = 50; ms <= 1000; ms += 50) {
    const lauf = t.fliessend(ms);
    z = fliessend(z, lauf, (ms - letzte) / 1000);
    letzte = ms;
  }
  // flüssig ab 250 ms: 750 ms · 1.4 m/s = 1.05 m (+ 50 ms des ersten Bildes, in dem die Taste die Schwelle erreicht: 0.05 s zählt mit)
  const weg = z0.z - z.z - 0.1;
  assert.ok(weg > 1.0 && weg < 1.2, `Weg ${weg}`);
});

// ------------------------------------------------------------------------------------------------ Einblendung

test('Einblendung: Raumkennzeichen, Nummer und Name, Geschoss, Augenhöhe in m', () => {
  const z = zustandAusStandort(standort());
  const e = einblendung(standort(), z);
  assert.equal(e.titel, 'R-1 · 0.01 · Wohnen');
  assert.equal(e.geschoss, 'Geschoss EG – Erdgeschoss');
  assert.equal(e.augenhoehe, 'Augenhöhe 1.70 m über OK Boden');
  const o = einblendung({ raum: 'R-2', name: null, geschoss: 'OG' }, augenhoeheAendern(z, 100));
  assert.equal(o.titel, 'R-2'); assert.equal(o.geschoss, 'Geschoss OG'); assert.equal(o.augenhoehe, 'Augenhöhe 1.80 m über OK Boden');
  assert.equal(einblendung({}, z).geschoss, '');
});

test('Projektwerte für die Kontrolle: Standort in mm, Azimut 0 … 360, Nicken in Grad', () => {
  const p = projektWerte(umsehen(zustandAusStandort(standort(90)), 4 * 90, -40));   // 90° nach rechts schauen, 10° nach oben
  assert.equal(p.x, 2000); assert.equal(p.y, 1500); assert.equal(p.z, 1700);
  nah(p.azimutGrad, 0, 1e-6); nah(p.nickenGrad, 10, 1e-6);
});
