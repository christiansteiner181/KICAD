// Tests des 3D-Schnittbereichs (N13) ohne Browser: schnittbereich-logik.js (Ebenen aus dem Quader, Griffe, Ziehen, Quader aus Punkten,
// Übernahme der alten Startansicht) und schnittbereich-palette.js (Markup der Eigenschaftenpalette).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/schnittbereich.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  schnittbereichLesen, fuerSpeichern, gleich, achsen, ebenen, enthaelt, weltPunkt, ecken, weltGrenzen, griffe, flaecheSchieben, achsenParameter,
  quaderSammler, aufGeschoss, winkelNormal, massText, modellPunkt, startansichtWert, startansichtUebernahme, MIN_KANTE, RAND_AUSWAHL, RAND_RAUM,
} from './schnittbereich-logik.js';
import { startansichtLesen, gebaeudeAusrichtungGrad } from './wuerfel-logik.js';
import { schnittbereichZeilen, startansichtText } from '../oberflaeche/schnittbereich-palette.js';

const GRAD = Math.PI / 180;
const nah = (a, b, eps = 1e-9, t = '') => assert.ok(Math.abs(a - b) < eps, `${t} ${a} ≠ ${b}`);
const BOX = (winkel = 0) => ({ aktiv: true, min: [1000, 2000, 0], max: [5000, 6000, 3000], winkel });

// ------------------------------------------------------------------------------------------------ Lesen und Vergleichen

test('schnittbereichLesen: gültige Form, Kopie, ungültige Formen ergeben null', () => {
  const roh = { aktiv: true, min: [0, 0, 0], max: [1000, 2000, 3000], winkel: 12 };
  const sb = schnittbereichLesen(roh);
  assert.deepEqual(sb, roh);
  sb.min[0] = 99;
  assert.equal(roh.min[0], 0, 'Kopie');
  assert.equal(schnittbereichLesen({ min: [0, 0, 0], max: [1, 1, 1] }).aktiv, false, 'ohne aktiv = aus');
  assert.equal(schnittbereichLesen({ min: [0, 0, 0], max: [1, 1, 1] }).winkel, 0);
  for (const schlecht of [null, undefined, 5, {}, { min: [0, 0], max: [1, 1, 1] }, { min: [0, 0, 0], max: [0, 1, 1] }, { min: [0, 0, 0], max: [1, 1, 'x'] }, { min: [2, 0, 0], max: [1, 1, 1] }])
    assert.equal(schnittbereichLesen(schlecht), null, JSON.stringify(schlecht));
});

test('fuerSpeichern rundet wie der Kern (0.1 mm, Winkel 4 Stellen); gleich vergleicht darauf', () => {
  const a = fuerSpeichern({ aktiv: true, min: [0.04, -0.04, 1.26], max: [100.05, 100, 200], winkel: 36.99996 });
  assert.deepEqual(a.min, [0, 0, 1.3]);
  assert.equal(a.winkel, 37);
  assert.ok(gleich(BOX(), { ...BOX(), min: [1000.01, 2000, 0] }));
  assert.ok(!gleich(BOX(), { ...BOX(), aktiv: false }));
  assert.ok(!gleich(BOX(), { ...BOX(), winkel: 1 }));
  assert.ok(gleich(null, null));
  assert.ok(!gleich(BOX(), null));
});

// ------------------------------------------------------------------------------------------------ Ebenen

test('achsen: u, v, z in three-Koordinaten; Winkel 0 = Modellachsen (x Ost, z = −Nord)', () => {
  const a = achsen(0);
  assert.deepEqual(a.u, [1, 0, -0]);
  assert.deepEqual(a.v.map((x) => x + 0), [-0 + 0, 0, -1]);
  const b = achsen(90);   // u zeigt nach Nord (Modell +y = three −z)
  nah(b.u[0], 0, 1e-12); nah(b.u[2], -1, 1e-12);
});

test('ebenen: sechs Ebenen, Normalen ins Innere, Abstand 0 auf der Fläche; ein Punkt in der Mitte liegt auf allen positiven Seiten', () => {
  for (const w of [0, 37, -52, 180]) {
    const sb = BOX(w);
    const e = ebenen(sb);
    assert.equal(e.length, 6);
    const mitte = weltPunkt(sb, 3000, 4000, 1500);
    for (const p of e) {
      const d = p.normale[0] * mitte.x + p.normale[1] * mitte.y + p.normale[2] * mitte.z + p.konstante;
      assert.ok(d > 0, `${p.name} @${w}°: Mitte innen (${d})`);
      assert.ok(Math.abs(Math.hypot(...p.normale) - 1) < 1e-12);
    }
    // Abstand der Mitte zu den Flächen = halbe Kantenlänge (in m)
    const halb = { u: 2, v: 2, z: 1.5 };
    for (const p of e) {
      const d = p.normale[0] * mitte.x + p.normale[1] * mitte.y + p.normale[2] * mitte.z + p.konstante;
      nah(d, halb[p.achse], 1e-9, `${p.name} @${w}°`);
    }
  }
});

test('enthaelt: Punkte knapp innen/ausserhalb jeder Fläche, auch für den gedrehten Quader', () => {
  for (const w of [0, 37, -80]) {
    const sb = BOX(w);
    const mm = (u, v, z) => weltPunkt(sb, u, v, z);
    assert.ok(enthaelt(sb, mm(3000, 4000, 1500)));
    assert.ok(enthaelt(sb, mm(1000.5, 4000, 1500), 0));
    assert.ok(!enthaelt(sb, mm(990, 4000, 1500), 1e-3), 'u−');
    assert.ok(!enthaelt(sb, mm(5010, 4000, 1500), 1e-3), 'u+');
    assert.ok(!enthaelt(sb, mm(3000, 1990, 1500), 1e-3), 'v−');
    assert.ok(!enthaelt(sb, mm(3000, 6010, 1500), 1e-3), 'v+');
    assert.ok(!enthaelt(sb, mm(3000, 4000, -10), 1e-3), 'z−');
    assert.ok(!enthaelt(sb, mm(3000, 4000, 3010), 1e-3), 'z+');
    assert.ok(enthaelt(sb, mm(990, 4000, 1500), 0.02), 'Toleranz 20 mm');
  }
});

test('Modell → three → Quaderkoordinaten gleich wie im Kern (u = x·cos + y·sin, v = −x·sin + y·cos)', () => {
  const winkel = 37, x = 12345, y = -6789, z = 2200;
  const s = quaderSammler(winkel);
  s.add(x / 1000, z / 1000, -y / 1000);   // three: x = Ost, y = oben, z = −Nord
  const q = s.quader(0);
  const t = winkel * GRAD;
  nah(q.min[0], x * Math.cos(t) + y * Math.sin(t), 0.06);
  nah(q.min[1], -x * Math.sin(t) + y * Math.cos(t), 0.06);
  nah(q.min[2], z, 0.06);
  // und zurück
  const w = weltPunkt({ ...q, winkel }, q.min[0], q.min[1], q.min[2]);
  nah(w.x * 1000, x, 0.1); nah(-w.z * 1000, y, 0.1); nah(w.y * 1000, z, 0.1);
});

test('ecken und weltGrenzen: acht Ecken, umschliessende Achsenbox des gedrehten Quaders', () => {
  const sb = { aktiv: true, min: [0, 0, 0], max: [4000, 3000, 2000], winkel: 90 };
  assert.equal(ecken(sb).length, 8);
  const g = weltGrenzen(sb);
  // 90°: u zeigt nach Nord (three −z), v nach West (three −x)
  nah(g.min.x, -3, 1e-9); nah(g.max.x, 0, 1e-9); nah(g.min.z, -4, 1e-9); nah(g.max.z, 0, 1e-9); nah(g.min.y, 0, 1e-9); nah(g.max.y, 2, 1e-9);
});

// ------------------------------------------------------------------------------------------------ Griffe

test('griffe: sechs, je in der Mitte einer Fläche, Richtung nach aussen steht senkrecht auf der Fläche', () => {
  for (const w of [0, 37]) {
    const sb = BOX(w);
    const g = griffe(sb);
    assert.deepEqual(g.map((x) => x.name), ['u−', 'u+', 'v−', 'v+', 'z−', 'z+']);
    for (const h of g) {
      const e = ebenen(sb).find((p) => p.name === h.name);
      // der Griff liegt auf der Ebene seiner Fläche …
      nah(e.normale[0] * h.mitte.x + e.normale[1] * h.mitte.y + e.normale[2] * h.mitte.z + e.konstante, 0, 1e-9, h.name);
      // … und zeigt entgegen der Normale der Ebene (diese zeigt ins Innere)
      for (let i = 0; i < 3; i++) nah(h.nachAussen[i], -e.normale[i], 1e-12, h.name);
    }
    // z+ sitzt über der Mitte
    const oben = g[5].mitte;
    const m = weltPunkt(sb, 3000, 4000, 3000);
    nah(oben.x, m.x, 1e-9); nah(oben.y, m.y, 1e-9); nah(oben.z, m.z, 1e-9);
  }
});

test('flaecheSchieben: nach aussen/innen, nur die eine Fläche, Mindestkante', () => {
  const sb = BOX();
  const a = flaecheSchieben(sb, 'u', 1, 500);
  assert.deepEqual([a.min, a.max], [[1000, 2000, 0], [5500, 6000, 3000]]);
  const b = flaecheSchieben(sb, 'v', -1, -300);   // nach innen
  assert.deepEqual([b.min[1], b.max[1]], [2300, 6000]);
  const c = flaecheSchieben(sb, 'z', 1, -10000);   // weit über die Gegenseite hinaus: bleibt MIN_KANTE hoch
  assert.equal(c.max[2] - c.min[2], MIN_KANTE);
  const d = flaecheSchieben(sb, 'u', -1, -99999);
  assert.equal(d.max[0] - d.min[0], MIN_KANTE);
  assert.deepEqual(sb, BOX(), 'Eingabe bleibt unverändert');
});

test('achsenParameter: nächster Punkt der Achse zum Mausstrahl', () => {
  // Achse: durch (0,0,0) entlang +x; Strahl von oben bei x = 5 → t = 5
  nah(achsenParameter([5, 1, 0], [0, -1, 0], [0, 0, 0], [1, 0, 0]), 5, 1e-12);
  // Strahl schräg: von (0, 10, 0) Richtung (1, -1, 0)/√2 trifft die Achse bei x = 10
  nah(achsenParameter([0, 10, 0], [1, -1, 0], [0, 0, 0], [1, 0, 0]), 10, 1e-12);
  // windschief: Strahl bei z = 3 über der Achse, ebenfalls t = 7
  nah(achsenParameter([7, 0, 3], [0, 0, -1], [0, 0, 0], [1, 0, 0]), 7, 1e-12);
  // negative Richtung und verschobener Achsenpunkt
  nah(achsenParameter([2, 5, 0], [0, -1, 0], [10, 0, 0], [-1, 0, 0]), 8, 1e-12);
  // parallel: kein Ergebnis
  assert.equal(achsenParameter([0, 1, 0], [1, 0, 0], [0, 0, 0], [1, 0, 0]), null);
});

test('Ziehen eines Griffs: die Verschiebung folgt der Maus entlang der Achse, auch beim gedrehten Quader', () => {
  const sb = BOX(37);
  const h = griffe(sb)[1];   // u+
  const mitte = [h.mitte.x, h.mitte.y, h.mitte.z];
  const t0 = achsenParameter([mitte[0], mitte[1] + 20, mitte[2]], [0, -1, 0], mitte, h.nachAussen);
  // Maus 0.8 m weiter in Zugrichtung (Strahl von oben durch den verschobenen Punkt)
  const ziel = mitte.map((x, i) => x + h.nachAussen[i] * 0.8);
  const t1 = achsenParameter([ziel[0], ziel[1] + 20, ziel[2]], [0, -1, 0], mitte, h.nachAussen);
  nah(t1 - t0, 0.8, 1e-9);
  const neu = flaecheSchieben(sb, h.achse, h.seite, (t1 - t0) * 1000);
  nah(neu.max[0] - sb.max[0], 800, 1e-6);
  assert.equal(neu.winkel, 37);
});

// ------------------------------------------------------------------------------------------------ Quader aus Punkten, Geschoss

test('quaderSammler: kleinster Quader in den Achsen des Winkels, Rand in alle Richtungen, leer = null', () => {
  assert.equal(quaderSammler(0).quader(300), null);
  const s = quaderSammler(0);
  s.add(1, 0, -2); s.add(4, 3, -5);   // Modell x 1000…4000, y 2000…5000, z 0…3000
  assert.equal(s.anzahl, 2);
  assert.deepEqual(s.quader(0).min, [1000, 2000, 0]);
  assert.deepEqual(s.quader(0).max, [4000, 5000, 3000]);
  const q = s.quader(RAND_AUSWAHL);
  assert.deepEqual(q.min, [700, 1700, -300]);
  assert.deepEqual(q.max, [4300, 5300, 3300]);
  assert.equal(q.aktiv, true);
  assert.equal(RAND_AUSWAHL, 300);
  assert.equal(RAND_RAUM, 1500);
});

test('quaderSammler gedreht: ein um den Winkel gedrehtes Rechteck ergibt genau seine Masse (nicht das Hüllrechteck)', () => {
  const winkel = 37, t = winkel * GRAD, c = Math.cos(t), sn = Math.sin(t);
  const s = quaderSammler(winkel);
  for (const [u, v] of [[0, 0], [10, 0], [10, 6], [0, 6]]) {   // 10 × 6 m in den Achsen des Gebäudes, dann ins Modell gedreht
    const x = u * c - v * sn, y = u * sn + v * c;
    s.add(x, 2.5, -y);
  }
  const q = s.quader(0);
  nah(q.max[0] - q.min[0], 10000, 0.2);
  nah(q.max[1] - q.min[1], 6000, 0.2);
  nah(q.max[2] - q.min[2], 0, 0.2);
});

test('aufGeschoss: nur die Höhe wechselt (OK/UK des Geschosses), Grundriss und Winkel bleiben', () => {
  const q = aufGeschoss(BOX(20), 3000, 2800);
  assert.deepEqual([q.min, q.max, q.winkel], [[1000, 2000, 3000], [5000, 6000, 5800], 20]);
});

test('winkelNormal bringt Winkel nach −180 … 180; massText nennt die Kantenlängen', () => {
  assert.equal(winkelNormal(0), 0);
  assert.equal(winkelNormal(397), 37);
  assert.equal(winkelNormal(-190), 170);
  assert.equal(winkelNormal(180), -180);
  assert.equal(winkelNormal(undefined), 0);
  assert.equal(massText(BOX()), '4.00 × 4.00 × 3.00 m');
  assert.equal(massText(BOX(-37)), '4.00 × 4.00 × 3.00 m, −37.0°');
});

// ------------------------------------------------------------------------------------------------ Startansicht

test('modellPunkt: three (x, y oben, z = −Nord) → Modell-mm [x Ost, y Nord, z oben]', () => {
  assert.deepEqual(modellPunkt({ x: 1.5, y: 2, z: -3 }), [1500, 3000, 2000]);
});

test('startansichtWert: Richtung, Position, Ziel in Modell-mm und Projektion; startansichtLesen liest es wieder', () => {
  const w = startansichtWert({ azimutGrad: 31.5, polarGrad: 60 }, { x: 10, y: 5, z: 12 }, { x: 2, y: 1, z: -1 });
  assert.deepEqual(w, { azimutGrad: 31.5, polarGrad: 60, position: [10000, -12000, 5000], ziel: [2000, 1000, 1000], projektion: 'perspektive' });
  assert.deepEqual(startansichtLesen(w), { azimutGrad: 31.5, polarGrad: 60 });
});

test('startansichtUebernahme: Modell geht vor Browser; alter Browser-Wert wird einmal übernommen und aufgeräumt', () => {
  const modell = { azimutGrad: 10, polarGrad: 50, position: [0, 0, 0] };
  const lokal = { azimutGrad: 99, polarGrad: 70 };
  // Modell hat einen Wert: er gilt, der Browser-Wert wird nur entfernt
  let u = startansichtUebernahme(modell, lokal, startansichtLesen);
  assert.deepEqual(u, { wert: { azimutGrad: 10, polarGrad: 50 }, schreiben: false, aufraeumen: true });
  // Modell leer, Browser-Wert vorhanden: ins Modell schreiben, dann entfernen
  u = startansichtUebernahme(null, lokal, startansichtLesen);
  assert.deepEqual(u, { wert: { azimutGrad: 99, polarGrad: 70 }, schreiben: true, aufraeumen: true });
  assert.deepEqual(startansichtUebernahme(undefined, lokal, startansichtLesen).schreiben, true);
  // beides leer
  assert.deepEqual(startansichtUebernahme(null, null, startansichtLesen), { wert: null, schreiben: false, aufraeumen: false });
  // ungültiger Browser-Wert: nichts übernehmen, aber wegräumen
  u = startansichtUebernahme(null, { azimutGrad: 'x' }, startansichtLesen);
  assert.deepEqual(u, { wert: null, schreiben: false, aufraeumen: true });
  // nur Modell, nichts im Browser
  assert.deepEqual(startansichtUebernahme(modell, null, startansichtLesen).aufraeumen, false);
});

test('Gebäudeausrichtung bestimmt den Winkel des Quaders (−planwinkel, wie der Würfel)', () => {
  assert.equal(winkelNormal(gebaeudeAusrichtungGrad({ werte: { planwinkel: -37 } })), 37);
  assert.equal(winkelNormal(gebaeudeAusrichtungGrad(null, null)), 0);
});

// ------------------------------------------------------------------------------------------------ Palette

const H = {
  zeile: (label, feld, o = {}) => `<div class="z ${o.klasse ?? ''}"><span>${label}</span>${feld}</div>`,
  nurLesen: (t) => `<span class="nl">${t}</span>`,
};

test('Palette: Schalter, Knöpfe und Masse; ausgeschaltet und ohne Quader', () => {
  const an = schnittbereichZeilen({ aktiv: true, min: [0, 0, 0], max: [4000, 3000, 2000], winkel: 37 }, H).join('');
  assert.match(an, /class="ep-sb-schalter"[^>]*checked/);
  assert.match(an, /4\.00 × 3\.00 × 2\.00 m, 37\.0°/);
  for (const k of ['ep-sb-auswahl', 'ep-sb-geschoss', 'ep-sb-zurueck']) assert.ok(an.includes(k), k);
  const aus = schnittbereichZeilen({ aktiv: false, min: [0, 0, 0], max: [4000, 3000, 2000], winkel: 0 }, H).join('');
  assert.doesNotMatch(aus, /checked/);
  assert.match(aus, /ausgeschaltet/);
  const leer = schnittbereichZeilen(null, H).join('');
  assert.doesNotMatch(leer, /checked/);
  assert.match(leer, /ep-sb-schalter/);
  assert.match(leer, /ganze Modell/);
});

test('Palette: startansichtText', () => {
  assert.match(startansichtText(null), /Isometrie/);
  assert.match(startansichtText({ azimutGrad: 31.5, polarGrad: 60 }), /31\.5°.*60\.0°/);
  assert.match(startansichtText({ azimutGrad: 'x' }), /Isometrie/);
});
