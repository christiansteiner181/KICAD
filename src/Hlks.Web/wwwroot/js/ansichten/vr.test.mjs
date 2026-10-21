// Tests von «In VR begehen» (N23) – der Teil ohne Brille und ohne Browser: vr-logik.js (Rig, Sticks, Snap-Turn, Teleport, Knopfzustand, Standort ohne Raum)
// und die N22-Regel mit der Kopfposition (durchgang3d.js mit der Kopfposition als Messpunkt, so wie begehen3d.js es anschliesst).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/vr.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { register } from 'node:module';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const threeUrl = pathToFileURL(path.resolve(hier, '../../lib/three/three.module.js')).href;
register('data:text/javascript,' + encodeURIComponent(
  `export async function resolve(s, c, n) { if (s === 'three') return { url: ${JSON.stringify(threeUrl)}, shortCircuit: true }; return n(s, c); }`));

const V = await import('./vr-logik.js');
const L = await import('./begehen-logik.js');
const { erzeugeDurchgang } = await import('./durchgang3d.js');

const { SNAP_GRAD, SNAP_AN, SNAP_LOS, TELEPORT_REICHWEITE, IFC_GRENZE_DREIECKE, VR_TEXT, knopfZustand, sitzungsOptionen, drehe, kopfWelt, blickGierenWelt, rigStart, rigDrehen,
  rigAufZiel, rigVersetzen, stickBewegung, snapPruefen, teleportZiel, ifcAnfangsSichtbar, bildzeit, standortAusKamera, gierenVonRichtung } = V;
const GRAD = Math.PI / 180;
const nah = (a, b, eps = 1e-9, t = '') => assert.ok(Math.abs(a - b) < eps, `${t} ${a} ≠ ${b}`);
/** Winkeldifferenz auf (−π, π]. */
const wd = (a, b) => { let d = a - b; while (d > Math.PI) d -= 2 * Math.PI; while (d <= -Math.PI) d += 2 * Math.PI; return d; };

/** Antwort der Abfrage raum_begehen (wie in begehen.test.mjs). */
const standort = (azimut = 90, extra = {}) => ({
  raum: 'R-1', name: 'Wohnen', nummer: '0.01', geschoss: 'EG', geschossName: 'Erdgeschoss',
  standpunkt: { x: 2000, y: 1500, z: 1700, art: 'schwerpunkt', abstandZumRand: 1400 }, bodenOk: 250, augenhoehe: 1700,
  blick: { azimutGrad: azimut, richtung: [Math.cos(azimut * GRAD), Math.sin(azimut * GRAD), 0], nickenGrad: 0, planwinkel: 0 },
  ...extra,
});

// ------------------------------------------------------------------------------------------------ Knopf

test('Knopf: ohne WebXR und ohne Brille ausgegraut mit Hinweis, mit Brille bereit, in der Sitzung «VR beenden»', () => {
  const kein = knopfZustand({ xrVorhanden: false });
  assert.equal(kein.deaktiviert, true); assert.equal(kein.hinweis, VR_TEXT.keinBrowser);
  const ohne = knopfZustand({ xrVorhanden: true, unterstuetzt: false });
  assert.equal(ohne.deaktiviert, true);
  assert.equal(ohne.hinweis, 'Keine VR-Brille verbunden (Quest Link starten oder KICAD im Browser der Quest öffnen)');
  assert.equal(ohne.text, 'In VR begehen');
  assert.equal(knopfZustand({ xrVorhanden: true, unterstuetzt: null }).deaktiviert, true, 'noch nicht geprüft: nicht anklickbar');
  const gut = knopfZustand({ xrVorhanden: true, unterstuetzt: true });
  assert.deepEqual(gut, { text: 'In VR begehen', deaktiviert: false, hinweis: '' });
  assert.deepEqual(knopfZustand({ xrVorhanden: true, unterstuetzt: true, aktiv: true }), { text: 'VR beenden', deaktiviert: false, hinweis: '' });
  assert.equal(knopfZustand({ xrVorhanden: true, unterstuetzt: true, startet: true }).deaktiviert, true);
  assert.equal(knopfZustand().deaktiviert, true);
});

test('Sitzungsoptionen: Referenzraum local-floor verlangt, Hand-Tracking wenn möglich', () => {
  const o = sitzungsOptionen();
  assert.deepEqual(o.requiredFeatures, ['local-floor']);
  assert.ok(o.optionalFeatures.includes('hand-tracking'));
});

// ------------------------------------------------------------------------------------------------ Rig-Start aus raum_begehen

test('Rig-Start: der Kopf steht am Standpunkt, schaut in die Blickrichtung des Begehens, das Rig sitzt auf OK Boden', () => {
  const z = L.zustandAusStandort(standort(90));
  const r = rigStart(z);                                  // Kopf im Nullpunkt, blickt nach −z (Gieren 90° im Referenzraum)
  nah(r.y, 0.25, 1e-12, 'Rig-Höhe = OK Boden des Raums');
  nah(r.x, 2); nah(r.z, -1.5);
  nah(r.gieren, 0, 1e-12, 'Blick nach Norden = −z = Blick im Referenzraum: keine Drehung nötig');
  // beliebige Blickrichtungen und ein Kopf, der nicht im Nullpunkt steht und nicht genau nach −z blickt
  for (const az of [0, 30, 90, 135, 180, 270, 359]) {
    const zz = L.zustandAusStandort(standort(az));
    for (const kopf of [{ x: 0, z: 0, gieren: Math.PI / 2 }, { x: 0.3, z: -0.2, gieren: 70 * GRAD }, { x: -0.5, z: 0.4, gieren: -100 * GRAD }]) {
      const rg = rigStart(zz, kopf);
      const w = kopfWelt(rg, { x: kopf.x, y: 1.62, z: kopf.z });
      nah(w.x, zz.x, 1e-9, `Kopf x bei ${az}°`); nah(w.z, zz.z, 1e-9, `Kopf z bei ${az}°`);
      nah(w.y, 0.25 + 1.62, 1e-9, 'Höhe: Boden des Raums + echte Kopfhöhe (local-floor), nicht die eingestellte Augenhöhe');
      nah(wd(blickGierenWelt(rg, kopf.gieren), zz.gieren), 0, 1e-9, `Blick bei ${az}°`);
    }
  }
});

test('Rig-Start: das Gieren zählt wie beim Begehen am Bildschirm (Ost = 0, Nord = 90° = −z)', () => {
  // Kopf blickt im Referenzraum nach −z; Blick des Begehens nach Osten (+x): das Rig muss um −90° drehen (rechtsherum)
  const r = rigStart(L.zustandAusStandort(standort(0)));
  nah(r.gieren, -Math.PI / 2, 1e-12);
  const v = drehe({ x: 0, z: -1 }, r.gieren);          // Vorwärts des Referenzraums in der Welt
  nah(v.x, 1, 1e-12); nah(v.z, 0, 1e-12);
  assert.equal(Math.round(gierenVonRichtung({ x: 0, z: -1 }) / GRAD), 90);
  nah(gierenVonRichtung({ x: 1, z: 0 }) / GRAD, 0);
});

// ------------------------------------------------------------------------------------------------ Snap-Turn

test('Snap-Turn: 30° je Ausschlag, nach rechts = rechtsherum, erst nach Rückkehr zur Mitte der nächste', () => {
  assert.equal(SNAP_GRAD, 30);
  let s = { bereit: true };
  let r = snapPruefen(s, 0.4); assert.equal(r.schritt, 0); s = r;                // zu wenig
  r = snapPruefen(s, 0.9); assert.equal(r.schritt, -1, 'Stick rechts = nach rechts drehen'); s = r;
  r = snapPruefen(s, 1); assert.equal(r.schritt, 0, 'gehalten dreht nicht weiter'); s = r;
  r = snapPruefen(s, 0.5); assert.equal(r.schritt, 0, 'noch nicht zurück in der Mitte'); s = r;
  r = snapPruefen(s, 0.1); assert.equal(r.schritt, 0); assert.equal(r.bereit, true); s = r;
  r = snapPruefen(s, -0.8); assert.equal(r.schritt, 1, 'Stick links = nach links'); s = r;
  assert.ok(SNAP_AN > SNAP_LOS);
  assert.equal(snapPruefen(s, undefined).schritt, 0);
  assert.equal(snapPruefen(null, NaN).schritt, 0);
});

test('Snap-Turn dreht um den Kopf: der Kopf bleibt an seiner Stelle, die Blickrichtung ändert sich um 30°; zwölf Schritte = Ausgangslage', () => {
  const kopf = { x: 0.4, y: 1.7, z: -0.3 };
  let rg = rigStart(L.zustandAusStandort(standort(90)), { ...kopf, gieren: Math.PI / 2 });
  const w0 = kopfWelt(rg, kopf), g0 = blickGierenWelt(rg, Math.PI / 2);
  const rechts = rigDrehen(rg, kopf, -SNAP_GRAD);
  const w1 = kopfWelt(rechts, kopf);
  nah(w1.x, w0.x, 1e-9); nah(w1.z, w0.z, 1e-9); nah(w1.y, w0.y, 1e-12);
  nah(wd(blickGierenWelt(rechts, Math.PI / 2), g0), -30 * GRAD, 1e-9, 'nach rechts = Gieren nimmt ab');
  let r = rg;
  for (let i = 0; i < 12; i++) r = rigDrehen(r, kopf, SNAP_GRAD);
  nah(wd(r.gieren, rg.gieren), 0, 1e-9); nah(r.x, rg.x, 1e-9); nah(r.z, rg.z, 1e-9); nah(r.y, rg.y, 1e-12);
});

// ------------------------------------------------------------------------------------------------ Stick-Bewegung

test('Stick: vor = in Blickrichtung des Kopfes mit 1.4 m/s, nur waagrecht, Totzone, zurück und seitlich', () => {
  // Blick nach Osten (gieren 0): vor = +x
  let b = stickBewegung({ x: 0, y: -1 }, 0, 1);
  nah(b.dx, L.GESCHWINDIGKEIT, 1e-12); nah(b.dz, 0, 1e-12);
  // Blick nach Norden (90°): vor = −z
  b = stickBewegung({ x: 0, y: -1 }, 90 * GRAD, 1);
  nah(b.dx, 0, 1e-9); nah(b.dz, -L.GESCHWINDIGKEIT, 1e-9);
  // zurück
  b = stickBewegung({ x: 0, y: 1 }, 90 * GRAD, 0.5);
  nah(b.dz, +0.7, 1e-9);
  // rechts bei Blick nach Norden = +x (Osten)
  b = stickBewegung({ x: 1, y: 0 }, 90 * GRAD, 1);
  nah(b.dx, L.GESCHWINDIGKEIT, 1e-9); nah(b.dz, 0, 1e-9);
  // Totzone
  assert.deepEqual(stickBewegung({ x: 0.1, y: -0.1 }, 0, 1), { dx: 0, dz: 0 });
  // halber Ausschlag (nach Totzone) ist langsamer, aber nicht null
  const halb = stickBewegung({ x: 0, y: -0.575 }, 0, 1);
  assert.ok(halb.dx > 0 && halb.dx < L.GESCHWINDIGKEIT);
  // schnell: dreifach; diagonal nicht schneller als geradeaus
  nah(stickBewegung({ x: 0, y: -1 }, 0, 1, true).dx, L.GESCHWINDIGKEIT * L.SCHNELL, 1e-12);
  const d = stickBewegung({ x: 1, y: -1 }, 0, 1);
  nah(Math.hypot(d.dx, d.dz), L.GESCHWINDIGKEIT, 1e-9, 'diagonal');
  // dt = 0 und kaputte Eingaben
  assert.deepEqual(stickBewegung({ x: 0, y: -1 }, 0, 0), { dx: 0, dz: 0 });
  assert.deepEqual(stickBewegung(undefined, 0, 1), { dx: 0, dz: 0 });
  assert.deepEqual(stickBewegung({ x: NaN, y: NaN }, 0, 1), { dx: 0, dz: 0 });
});

test('Stick in der Welt: das Rig geht in Blickrichtung des Kopfes, auch nach Snap-Turn; die Höhe ändert sich nie', () => {
  const kopf = { x: 0.1, y: 1.7, z: 0.1, gieren: Math.PI / 2 };
  let rg = rigStart(L.zustandAusStandort(standort(0)), kopf);             // Blick nach Osten
  const w0 = kopfWelt(rg, kopf);
  const b = stickBewegung({ x: 0, y: -1 }, blickGierenWelt(rg, kopf.gieren), 1);
  rg = rigVersetzen(rg, b.dx, b.dz);
  const w1 = kopfWelt(rg, kopf);
  nah(w1.x - w0.x, L.GESCHWINDIGKEIT, 1e-9); nah(w1.z - w0.z, 0, 1e-9); nah(w1.y, w0.y, 1e-12);
  // nach einem Schritt nach links (30°) geht es in die neue Richtung
  rg = rigDrehen(rg, kopf, SNAP_GRAD);
  const w2 = kopfWelt(rg, kopf);
  const b2 = stickBewegung({ x: 0, y: -1 }, blickGierenWelt(rg, kopf.gieren), 1);
  rg = rigVersetzen(rg, b2.dx, b2.dz);
  const w3 = kopfWelt(rg, kopf);
  nah(Math.atan2(-(w3.z - w2.z), w3.x - w2.x), 30 * GRAD, 1e-9, 'Richtung 30° links von Osten');
  nah(w3.y, w2.y, 1e-12);
});

// ------------------------------------------------------------------------------------------------ Teleport

test('Teleport-Ziel: der Strahl trifft den Boden auf der Höhe des Geschosses, nicht die Ebene y = 0', () => {
  const boden = 3.05;                                          // OK Boden im 1. OG
  const z = teleportZiel({ x: 1, y: boden + 1.2, z: -2 }, { x: 0, y: -1, z: -1 }, boden);   // 45° nach unten und nach −z
  assert.ok(z);
  nah(z.y, boden, 1e-12); nah(z.x, 1, 1e-9); nah(z.z, -2 - 1.2, 1e-9); nah(z.distanz, 1.2 * Math.SQRT2, 1e-9);
  // nicht normierte Richtung gibt dasselbe
  const z2 = teleportZiel({ x: 1, y: boden + 1.2, z: -2 }, { x: 0, y: -10, z: -10 }, boden);
  nah(z2.z, z.z, 1e-9);
  // steil nach unten
  const s = teleportZiel({ x: 0, y: boden + 1, z: 0 }, { x: 0.001, y: -1, z: 0 }, boden);
  nah(s.x, 0.001, 1e-6);
});

test('Teleport-Ziel: nach oben, waagrecht, zu weit, unter dem Boden oder unvollständig gibt es kein Ziel', () => {
  const u = { x: 0, y: 1.5, z: 0 };
  assert.equal(teleportZiel(u, { x: 0, y: 1, z: -1 }, 0), null, 'nach oben');
  assert.equal(teleportZiel(u, { x: 0, y: 0, z: -1 }, 0), null, 'waagrecht');
  assert.equal(teleportZiel(u, { x: 0, y: -0.001, z: -1 }, 0), null, 'fast waagrecht trifft erst nach über 15 m');
  assert.equal(teleportZiel(u, { x: 0, y: -1, z: -1 }, 5), null, 'Boden liegt über dem Controller');
  assert.equal(teleportZiel(u, { x: 0, y: -1, z: 0 }, 1.5), null, 'Controller steht auf dem Boden');
  assert.equal(teleportZiel(u, { x: 0, y: 0, z: 0 }, 0), null);
  assert.equal(teleportZiel(null, { x: 0, y: -1, z: 0 }, 0), null);
  assert.equal(teleportZiel({ x: NaN, y: 1, z: 0 }, { x: 0, y: -1, z: 0 }, 0), null);
  // genau an der Reichweite
  const rw = TELEPORT_REICHWEITE;
  assert.ok(teleportZiel({ x: 0, y: 1, z: 0 }, { x: Math.sqrt(rw * rw - 1), y: -1, z: 0 }, 0)?.distanz <= rw + 1e-9);
  assert.equal(teleportZiel({ x: 0, y: 1, z: 0 }, { x: Math.sqrt((rw + 1) ** 2 - 1), y: -1, z: 0 }, 0), null);
});

test('Teleport setzt den Kopf auf das Ziel (auch bei gedrehtem Rig und seitlich stehendem Kopf) und das Rig auf die Bodenhöhe', () => {
  const kopf = { x: 0.25, y: 1.68, z: -0.15, gieren: 80 * GRAD };
  const rg = rigStart(L.zustandAusStandort(standort(33)), kopf);
  const ziel = { x: 7.5, z: -4.25 };
  const neu = rigAufZiel(rg, kopf, ziel, 3.1);
  const w = kopfWelt(neu, kopf);
  nah(w.x, ziel.x, 1e-9); nah(w.z, ziel.z, 1e-9);
  nah(neu.y, 3.1, 1e-12); nah(w.y, 3.1 + 1.68, 1e-12, 'die Augenhöhe bleibt die echte Kopfhöhe');
  nah(neu.gieren, rg.gieren, 1e-12, 'Teleport dreht nicht');
});

// ------------------------------------------------------------------------------------------------ Standort ohne Raum

test('Standort ohne Raum: am Kamerastandpunkt, Blick zum Ziel, Boden = tiefste Stelle; verträgt sich mit dem Begehen am Bildschirm', () => {
  const s = standortAusKamera({ position: { x: 10, y: 14, z: -20 }, ziel: { x: 10, y: 3, z: -30 } }, 0.5, 1700);
  assert.deepEqual(s.standpunkt, { x: 10000, y: 20000, z: 500, art: 'kamera' });
  assert.equal(s.bodenOk, 500);
  nah(s.blick.azimutGrad, 90, 1e-9, 'Ziel liegt bei −z = Nord');
  assert.equal(s.raum, null);
  const z = L.zustandAusStandort(s);
  nah(z.x, 10); nah(z.z, -20); nah(z.boden, 0.5); nah(z.augen, 1.7); nah(z.gieren, Math.PI / 2);
  assert.ok(L.einblendung(s, z).titel.length > 0);
  // Blick nach Osten
  nah(standortAusKamera({ position: { x: 0, y: 5, z: 0 }, ziel: { x: 8, y: 0, z: 0 } }, 0).blick.azimutGrad, 0, 1e-9);
  // Blick senkrecht von oben: nach Norden
  nah(standortAusKamera({ position: { x: 0, y: 5, z: 0 }, ziel: { x: 0, y: 0, z: 0 } }, 0).blick.azimutGrad, 90, 1e-9);
  assert.equal(standortAusKamera(null, 0), null);
  assert.equal(standortAusKamera({ position: { x: 0, y: 0, z: 0 } }, NaN), null);
});

// ------------------------------------------------------------------------------------------------ IFC und Bildzeit

test('IFC-Unterlage in VR: bis 300 000 Dreiecke sichtbar, darüber zuerst aus', () => {
  assert.equal(ifcAnfangsSichtbar(0), true);
  assert.equal(ifcAnfangsSichtbar(IFC_GRENZE_DREIECKE), true);
  assert.equal(ifcAnfangsSichtbar(IFC_GRENZE_DREIECKE + 1), false);
  assert.equal(ifcAnfangsSichtbar(undefined), true);
});

test('Bildzeit: gleitender Mittelwert über das Fenster, Maximum, Leeren', () => {
  const z = bildzeit(3);
  assert.equal(z.mittel, 0);
  [10, 20, 30, 40].forEach((v) => z.neu(v));
  assert.equal(z.anzahl, 3); nah(z.mittel, 30); assert.equal(z.maximum, 40);
  z.neu(NaN); z.neu(-1); assert.equal(z.anzahl, 3);
  z.leeren(); assert.equal(z.anzahl, 0);
});

// ------------------------------------------------------------------------------------------------ N22 mit der Kopfposition

const { formAusTeil, huellboxVon } = L;
const wand = { art: 'prisma', grundriss: [[0, 0], [4000, 0], [4000, 200], [0, 200]], unten: 0, oben: 2500, material: 'wand' };
const mat = (deckkraft) => ({ opacity: deckkraft, transparent: deckkraft < 1, depthWrite: deckkraft >= 1, needsUpdate: false });
function bauteil(id, teil, deckkraft) {
  const formen = [formAusTeil(teil)];
  const m = mat(deckkraft);
  return { m, g: { userData: { id, ebene: 'wand', materialien: [m], formen, huellbox: huellboxVon(formen) } } };
}

test('N22 in VR: Wände undurchsichtig, durchscheinend unter 500 mm zur KOPFposition – nicht zur Position der Kamera, nicht zur Rig-Mitte', () => {
  const w = bauteil('W1', wand, 0.3);                         // Wand entlang x bei z = 0 … −0.2, bis 2.5 m hoch
  // Rig 2 m vor der Wand; der Kopf steht im Referenzraum 1.8 m seitlich/zurück versetzt (physisch gegangen) und 1.7 m hoch
  let rg = { x: 2, y: 0, z: 2, gieren: 0 };
  const lokal = { x: 0, y: 1.7, z: 0 };
  // wie begehen3d.js: Messpunkt = Kopf der VR, solange die Sitzung läuft
  const messpunkt = { get position() { return kopfWelt(rg, lokal); } };
  const d = erzeugeDurchgang({ bauteile: () => [w.g], kamera: messpunkt, anfordern: () => {} });
  d.starten();
  assert.equal(w.m.opacity, 1); assert.equal(w.m.transparent, false, 'beim Start undurchsichtig');
  assert.equal(d.zustand().nah.length, 0);
  // der Nutzer geht (Rig versetzt) bis 0.3 m vor die Wand (Kopf bei z = 0.3): durchscheinend
  rg = rigVersetzen(rg, 0, -1.7);
  d.pruefen();
  assert.equal(w.m.transparent, true); assert.equal(w.m.opacity, 0.3);
  // genau 500 mm gelten noch als entfernt (streng kleiner)
  rg = { ...rg, z: 0.5 }; d.pruefen();
  assert.equal(w.m.transparent, false);
  rg = { ...rg, z: 0.4999 }; d.pruefen();
  assert.equal(w.m.transparent, true);
  // Kopf nach hinten (Teleport 3 m weg): wieder undurchsichtig
  rg = rigAufZiel(rg, lokal, { x: 2, z: 3 }, 0); d.pruefen();
  assert.equal(w.m.transparent, false);
  d.beenden();
});

test('N22 in VR: auch die Höhe des Kopfes zählt (Kopf über der Wand, 3D-Abstand)', () => {
  const w = bauteil('W3', wand, 0.3);
  const hoch = { x: 0, y: 3.2, z: 0 };                       // Wand endet bei 2.5 m
  const rg = { x: 2, y: 0, z: 0.1, gieren: 0 };
  const d = erzeugeDurchgang({ bauteile: () => [w.g], kamera: { get position() { return kopfWelt(rg, hoch); } }, anfordern: () => {} });
  d.starten();
  assert.equal(w.m.transparent, false, '0.7 m über der Wand');
  hoch.y = 2.9; d.pruefen();
  assert.equal(w.m.transparent, true, '0.4 m über der Wand');
  d.beenden();
});

test('N22 in VR: Ende der Sitzung gibt die Darstellung zurück (Deckkraft, transparent, depthWrite wie vorher)', () => {
  const w = bauteil('W2', wand, 0.28);
  const orig = { opacity: w.m.opacity, transparent: w.m.transparent, depthWrite: w.m.depthWrite };
  const kopfPos = { x: 2, y: 1.7, z: 2 };
  const d = erzeugeDurchgang({ bauteile: () => [w.g], kamera: { position: kopfPos }, anfordern: () => {} });
  d.starten();
  assert.equal(w.m.transparent, false);
  kopfPos.z = 0.1; d.pruefen();
  assert.equal(w.m.transparent, true);
  d.beenden();
  assert.deepEqual({ opacity: w.m.opacity, transparent: w.m.transparent, depthWrite: w.m.depthWrite }, orig);
});

// ------------------------------------------------------------------------------------------------ Rig im three-Szenegraph

test('Rig im Szenegraphen (three): Kamera als Kind des Rigs steht am Standpunkt und schaut in die Blickrichtung – Vorzeichen von Gieren und rotation.y stimmen', async () => {
  const THREE = await import(threeUrl);
  for (const az of [0, 45, 90, 200, 300]) {
    const z = L.zustandAusStandort(standort(az));
    const kopf = { x: 0.2, y: 1.62, z: -0.1, gieren: 100 * GRAD };   // Pose, die die Brille liefert (lokal im Referenzraum)
    const rg = rigStart(z, kopf);
    const rigObj = new THREE.Group(); rigObj.position.set(rg.x, rg.y, rg.z); rigObj.rotation.set(0, rg.gieren, 0);
    const cam = new THREE.PerspectiveCamera(); rigObj.add(cam);
    cam.position.set(kopf.x, kopf.y, kopf.z); cam.rotation.set(0, kopf.gieren - Math.PI / 2, 0);   // wie updateUserCamera des WebXRManagers
    rigObj.updateMatrixWorld(true);
    const p = new THREE.Vector3().setFromMatrixPosition(cam.matrixWorld);
    nah(p.x, z.x, 1e-9, `x bei ${az}°`); nah(p.z, z.z, 1e-9, `z bei ${az}°`); nah(p.y, z.boden + 1.62, 1e-9);
    const d = new THREE.Vector3(0, 0, -1).transformDirection(cam.matrixWorld);
    nah(wd(gierenVonRichtung({ x: d.x, z: d.z }), z.gieren), 0, 1e-9, `Blick bei ${az}°`);
    // dieselbe Rechnung wie kopfWelt / blickGierenWelt
    const w = kopfWelt(rg, kopf);
    nah(w.x, p.x, 1e-9); nah(w.z, p.z, 1e-9); nah(w.y, p.y, 1e-9);
    nah(wd(blickGierenWelt(rg, kopf.gieren), gierenVonRichtung({ x: d.x, z: d.z })), 0, 1e-9);
  }
});
