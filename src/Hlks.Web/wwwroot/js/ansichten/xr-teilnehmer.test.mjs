// Tests der Anzeige «PC sieht die Brille» (Modul XR, Quest App P7) ohne Browser: Pose → Figur (Koordinaten, Blickrichtung, Hände, Name), Takt der Abfrage, Verdrahtung.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/xr-teilnehmer.test.mjs
// Die Posen haben die Form der Antwort von GET /api/xr/teilnehmer (Server: XrKanal.cs, XrNachrichten.cs): KICAD-mm, z oben, Quaternion [x, y, z, w]; Namen sind fiktiv.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as L from './xr-teilnehmer-logik.js';

const lies = (n) => readFileSync(new URL(n, import.meta.url), 'utf8');
const nah = (a, b, eps = 1e-6) => assert.ok(a.length === b.length && a.every((x, i) => Math.abs(x - b[i]) < eps), `${JSON.stringify(a)} ≠ ${JSON.stringify(b)}`);
const W = Math.SQRT1_2;

const kopf = (pos = [1000, 2000, 1700], rot = [0, 0, 0, 1]) => ({ pos, rot });
const eintrag = (extra = {}) => ({
  id: 't1', name: 'Quest 3', geraet: 'Meta Quest 3', kopplung: 'k1', brille: '1WMHH000000000', brillenName: 'Quest Christian', seit: '2026-10-23T20:00:00Z',
  pose: { kopf: kopf(), modus: 'begehen' }, poseZeit: new Date(1_000_000).toISOString(), ...extra,
});

test('Punkte: KICAD-mm (z oben) → three-Meter (y oben, z = −y)', () => {
  assert.deepEqual(L.punktNachThree([1000, 2000, 3000]), [1, 3, -2]);
  assert.deepEqual(L.punktNachThree([0, 0, 0]), [0, 0, -0]);
  assert.equal(L.punktNachThree([1, 2]), null);
  assert.equal(L.punktNachThree([1, 2, NaN]), null);
  assert.equal(L.punktNachThree(null), null);
  assert.deepEqual(L.richtungNachThree([0, 1, 0]), [0, 0, -1]);   // Norden zeigt in three nach −z
});

test('Quaternion dreht Vektoren (Rechtssystem, Gegenuhrzeigersinn um die Achse)', () => {
  nah(L.dreheVektor([0, 0, 0, 1], [0, 1, 0]), [0, 1, 0]);
  nah(L.dreheVektor([0, 0, W, W], [0, 1, 0]), [-1, 0, 0]);     // +90° um z: Norden → Westen
  nah(L.dreheVektor([0, 0, -W, W], [0, 1, 0]), [1, 0, 0]);     // −90° um z: Norden → Osten
  nah(L.dreheVektor([W, 0, 0, W], [0, 1, 0]), [0, 0, 1]);      // +90° um x: Norden → oben
  nah(L.dreheVektor([0, 0, 2 * W, 2 * W], [0, 1, 0]), [-1, 0, 0]);   // wird normiert
  nah(L.dreheVektor([0, 0, 0, 0], [0, 1, 0]), [0, 1, 0]);      // Nullquaternion: unverändert
  assert.equal(L.dreheVektor([0, 0], [0, 1, 0]), null);
});

test('Kopf ohne Drehung: Position in three-Metern, Blick nach Norden (−z), oben ist oben', () => {
  const f = L.kopfFigur(kopf());
  nah(f.pos, [1, 1.7, -2]);
  nah(f.blick, [0, 0, -1]);
  nah(f.oben, [0, 1, 0]);
});

test('Kopf gedreht: Blick nach Osten, Süden, Westen und nach oben', () => {
  nah(L.kopfFigur(kopf(undefined, [0, 0, -W, W])).blick, [1, 0, 0]);    // Osten = +x
  nah(L.kopfFigur(kopf(undefined, [0, 0, 1, 0])).blick, [0, 0, 1]);     // 180°: Süden = +z
  nah(L.kopfFigur(kopf(undefined, [0, 0, W, W])).blick, [-1, 0, 0]);    // Westen
  const hoch = L.kopfFigur(kopf(undefined, [W, 0, 0, W]));              // Kopf in den Nacken: Blick nach oben, «oben» nach Süden
  nah(hoch.blick, [0, 1, 0]);
  nah(hoch.oben, [0, 0, 1]);
});

test('Blickwinkel waagrecht in Grad: 0 Norden, 90 Osten', () => {
  assert.equal(L.blickGrad(kopf()), 0);
  assert.equal(L.blickGrad(kopf(undefined, [0, 0, -W, W])), 90);
  assert.equal(L.blickGrad(kopf(undefined, [0, 0, 1, 0])), 180);
  assert.equal(L.blickGrad(kopf(undefined, [0, 0, W, W])), 270);
  assert.equal(L.blickGrad(kopf(undefined, [W, 0, 0, W])), null);   // senkrecht nach oben: keine waagrechte Richtung
  assert.equal(L.blickGrad({ rot: 'x' }), null);
});

test('Gegenprobe mit der Umrechnung der App (KicadUmrechnung.DrehungZuKicad: [x, −z, y, w])', () => {
  // Die App dreht den Kopf um 90° nach links (StereoKit: um y, Blick vorher −Z, nachher −X = Westen) und sendet [x, −z, y, w].
  const qApp = [0, W, 0, W];
  const kicad = [qApp[0], -qApp[2], qApp[1], qApp[3]];
  nah(L.dreheVektor(kicad, [0, 1, 0]), [-1, 0, 0]);       // Westen in KICAD-Achsen (−x)
  assert.equal(L.blickGrad({ rot: kicad }), 270);
  nah(L.kopfFigur({ pos: [0, 0, 0], rot: kicad }).blick, [-1, 0, 0]);   // in three ebenfalls −x
});

test('Hände: Gelenk und Spitze in three-Metern, Pinch; nicht erkannt oder ungültig = null', () => {
  const h = L.handFigur({ gelenk: { pos: [800, 2000, 1300], rot: [0, 0, 0, 1] }, zeigefinger: [850, 2100, 1320], pinch: true, pinchStaerke: 0.9 }, 'links');
  assert.equal(h.seite, 'links');
  nah(h.gelenk, [0.8, 1.3, -2]);
  nah(h.spitze, [0.85, 1.32, -2.1]);
  assert.equal(h.pinch, true);
  assert.equal(L.handFigur({ gelenk: { pos: [1, 2, 3], rot: [0, 0, 0, 1] }, zeigefinger: null, pinch: false }, 'rechts').spitze, null);
  assert.equal(L.handFigur(null, 'links'), null);
  assert.equal(L.handFigur({ gelenk: { pos: [1, 2] } }, 'links'), null);
});

test('Farbe: stabil je Id, aus der Palette', () => {
  assert.equal(L.farbeFuer('t1'), L.farbeFuer('t1'));
  assert.ok(L.FARBEN.includes(L.farbeFuer('t17')));
  assert.ok(new Set(['t1', 't2', 't3', 't4', 't5']).size === 5);
  assert.ok(L.FARBEN.includes(L.farbeFuer(undefined)));
});

test('Etikett: Steuerzeichen weg, gekürzt, nie leer; Markup bleibt Text', () => {
  assert.equal(L.etikettText('Quest\nChristian\t1'), 'Quest Christian 1');
  assert.equal(L.etikettText('  '), 'Brille');
  assert.equal(L.etikettText(null), 'Brille');
  assert.equal(L.etikettText('x'.repeat(100)).length, L.NAME_MAX);
  assert.ok(L.etikettText('x'.repeat(100)).endsWith('…'));
  assert.equal(L.etikettText('<img src=x onerror=alert(1)>'), '<img src=x onerror=alert(1)>');   // wird nie als HTML ausgegeben (Canvas), nur nicht verändert
});

test('Teilnehmer → Figur: Name aus dem Namen der Kopplung, Etikett über dem Kopf, Modus', () => {
  const f = L.teilnehmerFigur(eintrag({ pose: { kopf: kopf(), links: { gelenk: { pos: [800, 2000, 1300], rot: [0, 0, 0, 1] }, zeigefinger: [850, 2100, 1320], pinch: false }, modus: 'sitzungsraum' } }), 1_000_100);
  assert.equal(f.id, 't1');
  assert.equal(f.name, 'Quest Christian');
  nah(f.etikett.pos, [1, 2.0, -2]);
  assert.equal(f.etikett.text, 'Quest Christian');
  assert.equal(f.haende.length, 1);
  assert.equal(f.haende[0].seite, 'links');
  assert.equal(f.modus, 'sitzungsraum');
  assert.equal(f.veraltet, false);
  assert.equal(f.alterMs, 100);
  assert.equal(L.teilnehmerFigur(eintrag({ brillenName: undefined })).name, 'Quest 3');   // ohne Namen der Kopplung der Name aus «hallo»
});

test('Sitzungsraum: die Brille steht am Tisch, die Figur ist als «am Tisch» gekennzeichnet und wird nicht ins Modell gezeichnet', () => {
  assert.equal(L.teilnehmerFigur(eintrag()).amTisch, false);
  const f = L.teilnehmerFigur(eintrag({ pose: { kopf: kopf(), modus: 'sitzungsraum', massstab: 50 } }));
  assert.equal(f.amTisch, true);
  assert.equal(f.modus, 'sitzungsraum');
  assert.match(lies('./xr-teilnehmer.js'), /filter\(\(f\) => !f\.amTisch\)/);
});

test('Veraltete Pose: nach 5 s blass, mit Hinweis im Etikett', () => {
  const f = L.teilnehmerFigur(eintrag(), 1_000_000 + L.VERALTET_MS + 1);
  assert.equal(f.veraltet, true);
  assert.match(L.figurText(f), /Quest Christian \(keine Pose seit 5 s\)/);
  assert.equal(L.figurText(L.teilnehmerFigur(eintrag(), 1_000_100)), 'Quest Christian');
  assert.equal(L.teilnehmerFigur(eintrag({ poseZeit: 'kaputt' }), 5).veraltet, false);   // ohne gültige Zeit keine Aussage
});

test('Ohne Pose oder mit kaputter Pose keine Figur', () => {
  assert.equal(L.teilnehmerFigur(eintrag({ pose: undefined })), null);
  assert.equal(L.teilnehmerFigur(eintrag({ pose: { kopf: { pos: [1, 2], rot: [0, 0, 0, 1] } } })), null);
  assert.equal(L.teilnehmerFigur(eintrag({ pose: { kopf: { pos: [1, 2, 3], rot: [0, 0, 0] } } })), null);
  assert.equal(L.teilnehmerFigur(null), null);
  assert.equal(L.teilnehmerFigur('x'), null);
});

test('Antwort des Servers: mehrere Brillen, Brillen ohne Pose fallen weg, Unsinn gibt eine leere Liste', () => {
  const a = { teilnehmer: [eintrag(), eintrag({ id: 't2', brillenName: 'Beat', pose: undefined }), eintrag({ id: 't3', brillenName: 'Anna' })], stellschrauben: {} };
  assert.deepEqual(L.figurenAus(a, 1_000_100).map((f) => f.id), ['t1', 't3']);
  assert.deepEqual(L.figurenAus({}), []);
  assert.deepEqual(L.figurenAus(null), []);
  assert.deepEqual(L.figurenAus({ teilnehmer: 'x' }), []);
});

test('Takt: mit Brille alle 200 ms, ohne Brille selten, ausgeschaltet oder versteckt keine Anfrage', () => {
  assert.deepEqual(L.abfragePlan({ an: true, anzahl: 1 }), { frage: true, nach: 200 });
  assert.deepEqual(L.abfragePlan({ an: true, anzahl: 0 }), { frage: true, nach: 4000 });
  assert.deepEqual(L.abfragePlan({ an: true }), { frage: true, nach: 4000 });
  assert.equal(L.abfragePlan({ an: false, anzahl: 3 }).frage, false);
  assert.equal(L.abfragePlan({ an: true, versteckt: true, anzahl: 3 }).frage, false);
  assert.equal(L.abfragePlan({ an: false }).nach, L.TAKT_AUS_MS);
  assert.equal(L.TAKT_MIT_BRILLE_MS, 200);
  assert.ok(L.TAKT_OHNE_BRILLE_MS >= 2000, 'ohne Brille keine Last');
});

test('Sichtbar nur eingeschaltet und mit mindestens einer Figur', () => {
  assert.equal(L.sichtbar(true, [{}]), true);
  assert.equal(L.sichtbar(true, []), false);
  assert.equal(L.sichtbar(false, [{}]), false);
  assert.equal(L.sichtbar(undefined, [{}]), true);
});

test('Verdrahtung: 3D-Ansicht, Aktion, Symbol, Einstellung', () => {
  const a3 = lies('./ansicht3d.js');
  assert.match(a3, /import \{ erzeugeBrillen \} from '\.\/xr-teilnehmer\.js'/);
  assert.match(a3, /const brillen = erzeugeBrillen\(\{/);
  assert.match(a3, /brillen\.zerstoeren\(\)/);
  assert.match(a3, /sichtbar: \(\) => !vr\?\.aktiv/, 'in VR bleibt die Figur aus');
  const akt = lies('../oberflaeche/aktionen-quest.js');
  assert.match(akt, /id: 'ansicht\.brilleZeigen'.*register: 'xr'.*gruppe: 'Brille'/s);
  assert.match(akt, /gedrueckt: \(\) => brilleZeigenAn\(\)/);
  assert.match(lies('../oberflaeche/symbole.js'), /brilleZeigen: '/);
  const t = lies('./xr-teilnehmer.js');
  assert.match(lies('./xr-brille-einstellung.js'), /EINSTELLUNG = 'ansicht3d\.brillen_zeigen'/);
  // das Menüband und der Dialog laden beim Start kein three.js: sie importieren nur das kleine Einstellungsmodul
  assert.doesNotMatch(lies('../oberflaeche/aktionen-quest.js'), /xr-teilnehmer\.js'/);
  assert.doesNotMatch(lies('../oberflaeche/quest-app-dialog.js'), /xr-teilnehmer\.js'/);
  assert.doesNotMatch(lies('./xr-brille-einstellung.js'), /from 'three'/);
  assert.match(t, /holen\('\/api\/xr\/teilnehmer'\)/);
  assert.doesNotMatch(t, /innerHTML/, 'Namen aus dem Netz gehen nie als HTML in die Seite (nur als Canvas-Text)');
  assert.match(t, /abfragePlan\(\{ an, versteckt: document\.hidden, anzahl \}\)\.frage/);
});

test('Die Figuren hängen an der Szene, nicht am Modell (kein Einpassen, kein Anklicken, kein Beschneiden)', () => {
  const t = lies('./xr-teilnehmer.js');
  assert.match(t, /szene\.add\(wurzel\)/);
  assert.doesNotMatch(t, /modell\.add/);
  const a3 = lies('./ansicht3d.js');
  assert.doesNotMatch(a3, /modell\.add\(brillen/);
});
