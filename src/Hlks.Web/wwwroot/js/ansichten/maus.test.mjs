// Tests der Maustasten der Ansichten (N10) und des Navigationswürfels (N11) – die Teile ohne Browser: maus.js und wuerfel-logik.js.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/maus.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  tasteAktion, orbitAktion, ORBIT_TASTEN, istDoppelklick, mitteVerschieben, ART_PLAN, ART_3D, DOPPELKLICK_MS,
} from './maus.js';
import {
  SCHWELLE, FLAECHEN, bereichVonPunkt, bereichsArt, bereichsName, richtungLokal, alleBereiche, teilflaechen, bereichsSchluessel,
  gebaeudeAusrichtungGrad, nordwinkelGrad, nordRichtungWelt, drehenUmHochachse, lokalZuWelt, weltZuLokal, kugelwinkel, richtungAusWinkeln, zielWinkel,
  winkelDifferenz, weich, zwischenstand, ziehenZuWinkel, nordAufRing, ringMarken, ringWinkel, ringZug,
  startansichtSchluessel, startansichtLesen, startansichtAusRichtung, startansichtZiel, schwenkZiel, START_VORGABE,
} from './wuerfel-logik.js';

const GRAD = Math.PI / 180;
const nah = (a, b, eps = 1e-9, t = '') => assert.ok(Math.abs(a - b) < eps, `${t} ${a} ≠ ${b}`);
const nahV = (a, b, eps = 1e-9, t = '') => { nah(a.x, b.x, eps, t + ' x'); nah(a.y, b.y, eps, t + ' y'); nah(a.z, b.z, eps, t + ' z'); };

// ------------------------------------------------------------------------------------------------ N10: Tasten

test('N10: in Plan-Ansichten verschiebt die mittlere Taste, links wählt, rechts Menü', () => {
  assert.equal(tasteAktion(ART_PLAN, 1), 'verschieben');
  assert.equal(tasteAktion(ART_PLAN, 1, { shift: true }), 'verschieben');
  assert.equal(tasteAktion(ART_PLAN, 0), 'auswahl');
  assert.equal(tasteAktion(ART_PLAN, 2), 'menue');
  assert.equal(tasteAktion(ART_PLAN, 3), null);
});

test('N10: im 3D wie Revit – Mitte verschiebt, Umschalt + Mitte dreht, links dreht, rechts verschiebt', () => {
  assert.equal(tasteAktion(ART_3D, 1), 'verschieben');
  assert.equal(tasteAktion(ART_3D, 1, { shift: true }), 'drehen');
  assert.equal(tasteAktion(ART_3D, 0), 'drehen');
  assert.equal(tasteAktion(ART_3D, 0, { shift: true }), 'verschieben');
  assert.equal(tasteAktion(ART_3D, 2), 'verschieben');
});

test('N10: die Belegung der OrbitControls ergibt für jede Taste und Umschalttaste dieselbe Aktion wie tasteAktion', () => {
  for (const button of [0, 1, 2]) for (const shift of [false, true]) for (const ctrl of [false, true]) {
    assert.equal(orbitAktion(ORBIT_TASTEN, button, { shift, ctrl }), tasteAktion(ART_3D, button, { shift, ctrl }), `Taste ${button} shift ${shift} ctrl ${ctrl}`);
  }
  assert.equal(ORBIT_TASTEN.MIDDLE, 'PAN', 'Mitte darf nicht mehr zoomen (DOLLY)');
});

test('N10: Doppelklick der mittleren Taste innerhalb von 400 ms', () => {
  assert.equal(istDoppelklick(0, 1000), false);
  assert.equal(istDoppelklick(1000, 1000 + DOPPELKLICK_MS - 1), true);
  assert.equal(istDoppelklick(1000, 1000 + DOPPELKLICK_MS), false);
});

/** Kleiner Ereignisempfänger mit Aufnahmephase wie ein Element, genug für mitteVerschieben. */
function falschesElement() {
  const h = new Map();
  return {
    h, erfasst: [],
    addEventListener(art, fn) { (h.get(art) ?? h.set(art, []).get(art)).push(fn); },
    removeEventListener(art, fn) { h.set(art, (h.get(art) ?? []).filter((x) => x !== fn)); },
    setPointerCapture(id) { this.erfasst.push(['an', id]); },
    releasePointerCapture(id) { this.erfasst.push(['aus', id]); },
    senden(art, e) { const ev = { type: art, stopped: false, prevented: false, stopPropagation() { this.stopped = true; }, preventDefault() { this.prevented = true; }, pointerId: 7, ...e }; for (const fn of h.get(art) ?? []) fn(ev); return ev; },
  };
}

test('N10: mitteVerschieben – Zug mit der mittleren Taste, andere Tasten bleiben unberührt, kein Autoscroll', () => {
  const el = falschesElement();
  let t = 0;
  const ereignisse = [];
  mitteVerschieben(el, { jetzt: () => t, beiBeginn: () => ereignisse.push('beginn'), beiBewegung: (dx, dy) => ereignisse.push(['bew', dx, dy]), beiEnde: () => ereignisse.push('ende'), beiDoppelklick: () => ereignisse.push('doppel') });
  const links = el.senden('pointerdown', { button: 0, clientX: 5, clientY: 5 });
  assert.equal(links.stopped, false, 'linke Taste geht weiter');
  assert.deepEqual(ereignisse, []);
  t = 1000;
  const mitte = el.senden('pointerdown', { button: 1, clientX: 10, clientY: 20 });
  assert.ok(mitte.stopped && mitte.prevented, 'mittlere Taste angehalten');
  el.senden('pointermove', { clientX: 14, clientY: 17 });
  el.senden('pointermove', { clientX: 14, clientY: 17 });   // keine Bewegung: kein Aufruf
  el.senden('pointermove', { clientX: 20, clientY: 17 });
  const oben = el.senden('pointerup', { button: 1, clientX: 20, clientY: 17 });
  assert.ok(oben.stopped);
  assert.deepEqual(ereignisse, ['beginn', ['bew', 4, -3], ['bew', 6, 0], 'ende']);
  assert.deepEqual(el.erfasst, [['an', 7], ['aus', 7]]);
  // zweiter Klick kurz danach = Doppelklick, kein neuer Zug
  t = 1200;
  el.senden('pointerdown', { button: 1, clientX: 0, clientY: 0 });
  assert.deepEqual(ereignisse.slice(4), ['doppel']);
  el.senden('pointermove', { clientX: 50, clientY: 50 });
  assert.equal(ereignisse.length, 5, 'nach dem Doppelklick keine Bewegung');
  // mousedown / auxclick der mittleren Taste: Autoscroll verhindern
  assert.ok(el.senden('mousedown', { button: 1 }).prevented);
  assert.ok(el.senden('auxclick', { button: 1 }).prevented);
  assert.equal(el.senden('mousedown', { button: 0 }).prevented, false);
});

// ------------------------------------------------------------------------------------------------ N11: Bereiche

test('N11: 6 Flächen, 12 Kanten, 8 Ecken; jede Teilfläche gehört zu genau einem Bereich', () => {
  const alle = alleBereiche();
  assert.equal(alle.length, 26);
  const arten = { flaeche: 0, kante: 0, ecke: 0 };
  for (const b of alle) arten[bereichsArt(b)]++;
  assert.deepEqual(arten, { flaeche: 6, kante: 12, ecke: 8 });
  const teile = teilflaechen();
  assert.equal(teile.length, 54);
  const nach = new Map();
  for (const t of teile) nach.set(bereichsSchluessel(t.bereich), (nach.get(bereichsSchluessel(t.bereich)) ?? 0) + 1);
  assert.equal(nach.size, 26);
  for (const b of alle) {
    const n = nach.get(bereichsSchluessel(b));
    assert.equal(n, { flaeche: 1, kante: 2, ecke: 3 }[bereichsArt(b)], bereichsName(b));   // eine Kante liegt auf 2 Flächen, eine Ecke auf 3
  }
  assert.equal(new Set(alle.map(bereichsName)).size, 26, 'Namen eindeutig');
});

test('N11: Namen deutsch – OBEN, UNTEN, VORNE, HINTEN, LINKS, RECHTS', () => {
  assert.deepEqual(FLAECHEN.map((f) => f.name).sort(), ['HINTEN', 'LINKS', 'OBEN', 'RECHTS', 'UNTEN', 'VORNE']);
  assert.equal(bereichsName({ x: 0, y: 1, z: 0 }), 'OBEN');
  assert.equal(bereichsName({ x: 0, y: -1, z: 0 }), 'UNTEN');
  assert.equal(bereichsName({ x: 0, y: 0, z: 1 }), 'VORNE');
  assert.equal(bereichsName({ x: 0, y: 0, z: -1 }), 'HINTEN');
  assert.equal(bereichsName({ x: -1, y: 0, z: 0 }), 'LINKS');
  assert.equal(bereichsName({ x: 1, y: 0, z: 0 }), 'RECHTS');
  assert.equal(bereichsName({ x: 1, y: 1, z: 1 }), 'OBEN-VORNE-RECHTS');
  assert.equal(bereichsName({ x: -1, y: 1, z: 0 }), 'OBEN-LINKS');
});

test('N11: Punkt auf der Oberfläche → Fläche, Kante oder Ecke (3 × 3 je Fläche)', () => {
  assert.deepEqual(bereichVonPunkt({ x: 0.1, y: 0.2, z: 1 }), { x: 0, y: 0, z: 1 });
  assert.deepEqual(bereichVonPunkt({ x: 0.9, y: 0.2, z: 1 }), { x: 1, y: 0, z: 1 });
  assert.deepEqual(bereichVonPunkt({ x: -0.9, y: 0.95, z: 1 }), { x: -1, y: 1, z: 1 });
  assert.deepEqual(bereichVonPunkt({ x: 1, y: -0.3, z: 0.7 }), { x: 1, y: 0, z: 1 });
  assert.deepEqual(bereichVonPunkt({ x: SCHWELLE - 0.01, y: 0, z: 1 }), { x: 0, y: 0, z: 1 });
  assert.deepEqual(bereichVonPunkt({ x: -1, y: -1, z: -1 }), { x: -1, y: -1, z: -1 });
});

test('N11: Teilflächen liegen auf der Oberfläche, nach aussen gewickelt und überlappen nicht', () => {
  for (const t of teilflaechen()) {
    const [a, b, c] = t.ecken;
    const n = { x: (b.y - a.y) * (c.z - a.z) - (b.z - a.z) * (c.y - a.y), y: (b.z - a.z) * (c.x - a.x) - (b.x - a.x) * (c.z - a.z), z: (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) };
    const f = FLAECHEN.find((x) => x.name === t.flaeche);
    assert.ok(n.x * f.normale[0] + n.y * f.normale[1] + n.z * f.normale[2] > 0, `${t.flaeche} nach aussen`);
    for (const p of t.ecken) assert.ok(Math.abs(p.x * f.normale[0] + p.y * f.normale[1] + p.z * f.normale[2] - 1) < 1e-9, 'liegt auf der Fläche');
    // die Mitte der Teilfläche ergibt wieder ihren Bereich
    const m = { x: (a.x + c.x) / 2, y: (a.y + c.y) / 2, z: (a.z + c.z) / 2 };
    assert.deepEqual(bereichVonPunkt(m), t.bereich);
  }
});

// ------------------------------------------------------------------------------------------------ N11: Kamerarichtungen

test('N11: Kamerarichtung je Fläche (ohne Ausrichtung): OBEN von oben, VORNE von Süden …', () => {
  const erwartet = {
    OBEN: [0, 1, 0], UNTEN: [0, -1, 0], VORNE: [0, 0, 1], HINTEN: [0, 0, -1], RECHTS: [1, 0, 0], LINKS: [-1, 0, 0],
  };
  for (const f of FLAECHEN) {
    const z = zielWinkel({ x: f.normale[0], y: f.normale[1], z: f.normale[2] }, 0);
    nahV(z.richtung, { x: erwartet[f.name][0], y: erwartet[f.name][1], z: erwartet[f.name][2] }, 1e-9, f.name);
  }
  assert.equal(zielWinkel({ x: 0, y: 1, z: 0 }).polar, 0, 'Draufsicht: Polarwinkel 0');
  nah(zielWinkel({ x: 0, y: -1, z: 0 }).polar, Math.PI, 1e-12, 'Untersicht');
  nah(zielWinkel({ x: 0, y: 0, z: 1 }).polar, Math.PI / 2, 1e-12, 'VORNE waagrecht');
});

test('N11: Kanten und Ecken – 45° bzw. Isometrie', () => {
  // Kante OBEN-VORNE: 45° nach oben, von vorne
  const k = zielWinkel({ x: 0, y: 1, z: 1 }, 0);
  nah(k.polar, 45 * GRAD, 1e-12, 'Polarwinkel Kante');
  nahV(k.richtung, { x: 0, y: Math.SQRT1_2, z: Math.SQRT1_2 });
  // Kante VORNE-RECHTS: waagrecht, 45° zwischen Süd und Ost
  const k2 = zielWinkel({ x: 1, y: 0, z: 1 }, 0);
  nah(k2.polar, 90 * GRAD, 1e-12);
  nah(k2.azimut, 45 * GRAD, 1e-12);
  // Ecke OBEN-VORNE-RECHTS: Isometrie, Polarwinkel acos(1/√3) ≈ 54.74°
  const e = zielWinkel({ x: 1, y: 1, z: 1 }, 0);
  nah(e.polar / GRAD, Math.acos(1 / Math.sqrt(3)) / GRAD, 1e-9, 'Isometrie');
  nah(e.azimut, 45 * GRAD, 1e-12);
  nahV(e.richtung, { x: 1 / Math.sqrt(3), y: 1 / Math.sqrt(3), z: 1 / Math.sqrt(3) });
  // alle 8 Ecken: gleicher Polarwinkel (oben bzw. unten), Azimute 45°, 135°, −135°, −45°
  const azimute = new Set();
  for (const x of [-1, 1]) for (const y of [-1, 1]) for (const z of [-1, 1]) {
    const w = zielWinkel({ x, y, z }, 0);
    nah(Math.min(w.polar, Math.PI - w.polar), Math.acos(1 / Math.sqrt(3)), 1e-9);
    azimute.add(Math.round(w.azimut / GRAD));
  }
  assert.deepEqual([...azimute].sort((a, b) => a - b), [-135, -45, 45, 135]);
  assert.equal(alleBereiche().filter((b) => bereichsArt(b) === 'kante').map((b) => zielWinkel(b, 0)).length, 12);
});

test('N11: VORNE folgt der Gebäudeausrichtung (eine Funktion), Vorgabe 0 = Projektachsen', () => {
  assert.equal(gebaeudeAusrichtungGrad(undefined), 0);
  assert.equal(gebaeudeAusrichtungGrad({ werte: {} }), 0);
  assert.equal(gebaeudeAusrichtungGrad({ werte: { planwinkel: 'abc' } }), 0);
  assert.equal(gebaeudeAusrichtungGrad({ werte: { planwinkel: 20 } }), -20, 'planwinkel dreht die Darstellung, die Gebäudekante liegt um −planwinkel');
  // VORNE bei Ausrichtung 30°: die Kamera steht senkrecht vor der gedrehten Front
  const theta = 30 * GRAD;
  const v = zielWinkel({ x: 0, y: 0, z: 1 }, theta);
  nahV(v.richtung, drehenUmHochachse({ x: 0, y: 0, z: 1 }, theta));
  // OBEN bleibt oben; der Azimut folgt der gedrehten Front (Bildschirm-oben = hinten)
  const o = zielWinkel({ x: 0, y: 1, z: 0 }, theta);
  assert.equal(o.polar, 0);
  nah(o.azimut, v.azimut, 1e-12);
  // RECHTS ist die um θ gedrehte x-Achse
  nahV(zielWinkel({ x: 1, y: 0, z: 0 }, theta).richtung, drehenUmHochachse({ x: 1, y: 0, z: 0 }, theta));
  // lokal ↔ Welt sind Umkehrungen
  const p = { x: 0.3, y: 0.5, z: -0.8 };
  nahV(weltZuLokal(lokalZuWelt(p, 0.7), 0.7), p);
  // Drehung gegen den Uhrzeigersinn von oben: +x → −z (Norden)
  nahV(drehenUmHochachse({ x: 1, y: 0, z: 0 }, Math.PI / 2), { x: 0, y: 0, z: -1 });
});

test('N11: Kugelwinkel und Richtung sind Umkehrungen, senkrechte Richtung nimmt den Rückfall-Azimut', () => {
  for (const [x, y, z] of [[1, 2, 3], [-2, 1, 0.5], [0, -1, 2], [3, 0.1, -4]]) {
    const l = Math.hypot(x, y, z);
    const w = kugelwinkel({ x, y, z });
    nahV(richtungAusWinkeln(w.azimut, w.polar), { x: x / l, y: y / l, z: z / l });
  }
  nah(kugelwinkel({ x: 0, y: 5, z: 0 }, 1.25).azimut, 1.25);
  assert.equal(richtungLokal({ x: 0, y: 2, z: 0 }).y, 1);
});

test('N11: weicher Übergang – kurzer Weg beim Azimut, Anfang und Ende stimmen', () => {
  const von = { azimut: 170 * GRAD, polar: 1, abstand: 10, ziel: { x: 0, y: 0, z: 0 } };
  const nach = { azimut: -170 * GRAD, polar: 2, abstand: 20, ziel: { x: 10, y: 0, z: -4 } };
  const a = zwischenstand(von, nach, 0), e = zwischenstand(von, nach, 1), m = zwischenstand(von, nach, 0.5);
  nah(a.azimut, von.azimut); nah(winkelDifferenz(e.azimut, nach.azimut), 0, 1e-9);
  nah(winkelDifferenz(m.azimut, 180 * GRAD), 0, 1e-9, 'Mitte liegt bei 180° (kurzer Weg)');
  nah(m.polar, 1.5); nah(m.abstand, 15); nah(m.ziel.x, 5);
  assert.equal(weich(0), 0); assert.equal(weich(1), 1); assert.equal(weich(0.5), 0.5); assert.equal(weich(-3), 0); assert.equal(weich(7), 1);
});

test('N11: Ziehen am Würfel – nach rechts dreht den Azimut zurück, nach unten hebt die Kamera', () => {
  const w = ziehenZuWinkel(10, 0);
  assert.ok(w.dAzimut < 0 && w.dPolar === 0);
  assert.ok(ziehenZuWinkel(0, 10).dPolar < 0);
  assert.ok(ziehenZuWinkel(-10, -10).dAzimut > 0);
});

// ------------------------------------------------------------------------------------------------ N11: Kompass

test('N11: Nordwinkel aus Projektinformation', () => {
  assert.equal(nordwinkelGrad(undefined), 0);
  assert.equal(nordwinkelGrad({ werte: {} }), 0);
  assert.equal(nordwinkelGrad({ werte: { nordwinkel: 35.5 } }), 35.5);
  assert.equal(nordwinkelGrad({ werte: { nordwinkel: 'x' } }), 0);
  // 0° = Plan genordet: Nord ist Projekt-Nord = −z
  nahV({ x: nordRichtungWelt(0).x, y: 0, z: nordRichtungWelt(0).z }, { x: 0, y: 0, z: -1 });
  // 90° im Uhrzeigersinn: geografisch Nord liegt im Osten des Projekts (+x)
  nahV({ x: nordRichtungWelt(90).x, y: 0, z: nordRichtungWelt(90).z }, { x: 1, y: 0, z: 0 }, 1e-12);
  nahV({ x: nordRichtungWelt(180).x, y: 0, z: nordRichtungWelt(180).z }, { x: 0, y: 0, z: 1 }, 1e-12);
});

test('N11: Kompassring – N steht oben, wenn man von Süden nach Norden blickt; O, S, W im Uhrzeigersinn', () => {
  const m = ringMarken(0, 0);   // Kamera auf der Südseite (Azimut 0), Nordwinkel 0
  nah(m.N, 0); nah(m.O, Math.PI / 2); nah(m.S, Math.PI); nah(m.W, 3 * Math.PI / 2);
  // Kamera im Osten (Azimut 90°), Blick nach Westen: Norden liegt rechts
  nah(ringMarken(90 * GRAD, 0).N, 90 * GRAD, 1e-12);
  // Kamera im Norden (Azimut 180°): Norden liegt unten
  nah(ringMarken(180 * GRAD, 0).N, 180 * GRAD, 1e-12);
  // Nordwinkel 30°: geografisch Nord liegt 30° im Uhrzeigersinn neben Projekt-Nord
  nah(ringMarken(0, 30).N, 30 * GRAD, 1e-12);
  nah(ringMarken(0, -30).N, 330 * GRAD, 1e-12, 'negativer Nordwinkel bleibt im Bereich 0 … 2π');
  nah(nordAufRing(-90 * GRAD, 0), 270 * GRAD, 1e-12);
});

test('N11: Kompassring – N zeigt zur geografischen Nordrichtung in der Welt', () => {
  // Für beliebige Azimute und Nordwinkel: die Bildschirmrichtung von Nord (rechts/oben der Kamera) stimmt mit dem Ringwinkel überein
  for (const az of [0, 40, 100, 200, -75]) for (const n of [0, 25, -40, 120]) {
    const a = az * GRAD;
    const d = nordRichtungWelt(n);
    const rechts = { x: Math.cos(a), z: -Math.sin(a) }, oben = { x: -Math.sin(a), z: -Math.cos(a) };   // von oben gesehen, Kamera auf der Seite (sin a, cos a)
    const sx = d.x * rechts.x + d.z * rechts.z, sy = d.x * oben.x + d.z * oben.z;
    nah(Math.atan2(sx, sy), ((nordAufRing(a, n) + Math.PI) % (2 * Math.PI)) - Math.PI, 1e-9, `az ${az} n ${n}`);
  }
});

test('N11: Ziehen am Ring dreht um die Hochachse – im Uhrzeigersinn erhöht den Azimut', () => {
  const mitte = { x: 100, y: 50 };
  const oben = ringWinkel({ x: 100, y: 20 }, mitte, 0.3);
  const rechts = ringWinkel({ x: 150, y: 50 }, mitte, 0.3);
  const unten = ringWinkel({ x: 100, y: 80 }, mitte, 0.3);
  nah(oben, 0); nah(rechts, Math.PI / 2); nah(Math.abs(unten), Math.PI, 1e-9);
  nah(ringZug(oben, rechts), Math.PI / 2);
  nah(ringZug(rechts, oben), -Math.PI / 2);
  nah(ringZug(170 * GRAD, -170 * GRAD), 20 * GRAD, 1e-9, 'über 180° hinweg kurzer Weg');
  // Der Azimut folgt dem Ringzug: Nord wandert mit
  const vorher = ringMarken(0, 0).N, nachher = ringMarken(ringZug(oben, rechts), 0).N;
  nah(nachher - vorher, Math.PI / 2);
});

// ------------------------------------------------------------------------------------------------ N10/N11: Orbit und Startansicht

test('N10: Orbit um den Drehpunkt – das Ziel wandert auf der Blicklinie, der Blick bleibt', () => {
  const pos = { x: 10, y: 5, z: 10 }, ziel = { x: 0, y: 0, z: 0 };
  const pivot = { x: 2, y: 1, z: -1 };
  const neu = schwenkZiel(pos, ziel, pivot);
  const blickAlt = { x: ziel.x - pos.x, y: ziel.y - pos.y, z: ziel.z - pos.z };
  const blickNeu = { x: neu.x - pos.x, y: neu.y - pos.y, z: neu.z - pos.z };
  const la = Math.hypot(blickAlt.x, blickAlt.y, blickAlt.z), ln = Math.hypot(blickNeu.x, blickNeu.y, blickNeu.z);
  nahV({ x: blickAlt.x / la, y: blickAlt.y / la, z: blickAlt.z / la }, { x: blickNeu.x / ln, y: blickNeu.y / ln, z: blickNeu.z / ln }, 1e-12, 'gleiche Richtung');
  // die Tiefe des Drehpunkts: gleicher Abstand zur Kamera entlang des Blicks
  const e = { x: blickAlt.x / la, y: blickAlt.y / la, z: blickAlt.z / la };
  nah(ln, (pivot.x - pos.x) * e.x + (pivot.y - pos.y) * e.y + (pivot.z - pos.z) * e.z, 1e-12);
  // Drehpunkt hinter der Kamera: Mindestabstand
  const hinten = schwenkZiel(pos, ziel, { x: 40, y: 20, z: 40 }, 0.05);
  assert.ok(Math.hypot(hinten.x - pos.x, hinten.y - pos.y, hinten.z - pos.z) >= 0.05 - 1e-12);
});

test('N11: Startansicht – Vorgabe Isometrie, gemerkte folgt der Gebäudeausrichtung, ungültige wird verworfen', () => {
  assert.equal(startansichtSchluessel('C:\\Proj\\A', 'AN-3D'), 'startansicht3d.c:\\proj\\a.AN-3D');
  assert.equal(startansichtLesen(null), null);
  assert.equal(startansichtLesen({ azimutGrad: 10, polarGrad: 200 }), null);
  assert.equal(startansichtLesen({ azimutGrad: 'x', polarGrad: 20 }), null);
  assert.deepEqual(startansichtLesen({ azimutGrad: 10, polarGrad: 60 }), { azimutGrad: 10, polarGrad: 60 });
  const v = startansichtZiel(null, 0);
  nahV(v.richtung, zielWinkel(START_VORGABE, 0).richtung);
  // merken und wiederherstellen ergibt dieselbe Richtung, auch mit Ausrichtung
  for (const theta of [0, 0.4, -1.1]) {
    const welt = richtungAusWinkeln(1.1, 0.9);
    const gemerkt = startansichtAusRichtung(welt, theta);
    const zurueck = startansichtZiel(gemerkt, theta);
    nahV(zurueck.richtung, welt, 2e-3, `theta ${theta}`);
  }
  // wird das Gebäude später gedreht, dreht die gemerkte Ansicht mit
  const gemerkt = startansichtAusRichtung(richtungAusWinkeln(0.5, 1), 0);
  const gedreht = startansichtZiel(gemerkt, 0.3);
  nah(gedreht.azimut - 0.5, 0.3, 2e-3);
});
