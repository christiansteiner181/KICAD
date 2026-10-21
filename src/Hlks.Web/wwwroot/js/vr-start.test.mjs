// Node-Test der VR-Startseite (Modul XR): Wahl des Startpunkts, Prüfung des Parameters ?raum=, gemerkter Standpunkt, Erdgeschoss, Anzeige.
// Aufruf: node --test vr-start.test.mjs
import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as L from './vr-start-logik.js';
import { urlText } from './oberflaeche/quest-text.js';

const GRAD = Math.PI / 180;
const ID = '3f2b8c1e-5a47-4d9e-b1c2-0a9d8e7f6b5a';

// ------------------------------------------------------------------------------------------ Parameter
test('raumParameter: gültige Id und Kennzeichen kommen durch', () => {
  assert.equal(L.raumParameter(`?raum=${ID}`), ID);
  assert.equal(L.raumParameter(`raum=${ID.toUpperCase()}`), ID.toUpperCase());
  assert.equal(L.raumParameter('?raum=R-1'), 'R-1');
  assert.equal(L.raumParameter('?raum=R.1.02_a'), 'R.1.02_a');
  assert.equal(L.raumParameter(new URLSearchParams({ raum: ID })), ID);
});

test('raumParameter: ungültig, fehlend oder mehrdeutig wird ignoriert', () => {
  for (const schlecht of ['', '?', '?x=1', '?raum=', '?raum=../../etc/passwd', '?raum=<script>', '?raum=R 1', '?raum=%3Cb%3E', '?raum=R-1;drop', '?raum=-R1', '?raum=' + 'a'.repeat(41),
    `?raum=${ID}&raum=${ID}`, '?raum=R-1&raum=R-2', `?raum=${ID}!`, '?raum=%00']) {
    assert.equal(L.raumParameter(schlecht), null, schlecht);
  }
  assert.equal(L.raumParameter(null), null);
  assert.equal(L.raumParameter(undefined), null);
});

test('raumGueltig: nur Text mit dem Muster', () => {
  assert.equal(L.raumGueltig(ID), true);
  assert.equal(L.raumGueltig('R-1'), true);
  assert.equal(L.raumGueltig(''), false);
  assert.equal(L.raumGueltig(5), false);
  assert.equal(L.raumGueltig(null), false);
  assert.equal(L.raumGueltig('a/b'), false);
});

// ------------------------------------------------------------------------------------------ gemerkter Standpunkt
const ORT = 'C:\\Projekte\\Musterweg\\e_KICAD';
const vorher = { raum: 'R-3', name: 'Wohnen', nummer: '1.03', geschoss: 'EG', geschossName: 'Erdgeschoss' };

test('standpunktMerken: Kopfstand in Projektmillimeter und Grad', () => {
  // three: x = Ost, z = −Nord; Gieren 90° = Projekt-Nord
  const g = L.standpunktMerken({ x: 4.25, z: -7.5, boden: 0.02, gieren: 90 * GRAD }, vorher, ORT, '2026-10-21T10:00:00Z');
  assert.equal(g.ort, ORT);
  assert.equal(g.x, 4250);
  assert.equal(g.y, 7500);
  assert.equal(g.bodenOk, 20);
  assert.equal(g.azimutGrad, 90);
  assert.equal(g.raum, 'R-3');
  assert.equal(g.geschossName, 'Erdgeschoss');
  assert.equal(g.zeit, '2026-10-21T10:00:00Z');
});

test('standpunktMerken: Winkel auf 0 … 360 gebracht', () => {
  assert.equal(L.standpunktMerken({ x: 0, z: 0, boden: 0, gieren: -90 * GRAD }, null, ORT).azimutGrad, 270);
  assert.equal(L.standpunktMerken({ x: 0, z: 0, boden: 0, gieren: 450 * GRAD }, null, ORT).azimutGrad, 90);
});

test('standpunktMerken: ohne brauchbare Angaben nichts', () => {
  assert.equal(L.standpunktMerken(null, vorher, ORT), null);
  assert.equal(L.standpunktMerken({ x: 1, z: 1, boden: 0 }, vorher, ORT), null);
  assert.equal(L.standpunktMerken({ x: NaN, z: 1, boden: 0, gieren: 0 }, vorher, ORT), null);
  assert.equal(L.standpunktMerken({ x: 1, z: 1, boden: 0, gieren: 0 }, vorher, ''), null);
  assert.equal(L.standpunktMerken({ x: 1, z: 1, boden: 0, gieren: 0 }, vorher, undefined), null);
});

test('Merken und Lesen: derselbe Standpunkt kommt als Standort für das Begehen zurück', () => {
  const g = L.standpunktMerken({ x: 4.25, z: -7.5, boden: 0.02, gieren: 135 * GRAD }, vorher, ORT);
  const s = L.standortAusGemerkt(JSON.parse(JSON.stringify(g)), ORT);   // über die Benutzereinstellung läuft es als JSON
  assert.equal(s.standpunkt.x, 4250);
  assert.equal(s.standpunkt.y, 7500);
  assert.equal(s.bodenOk, 20);
  assert.equal(s.blick.azimutGrad, 135);
  assert.equal(s.blick.nickenGrad, 0);
  assert.equal(s.raum, 'R-3');
  assert.equal(s.geschossName, 'Erdgeschoss');
});

test('standortAusGemerkt: anderes Projekt, kaputte oder fremde Werte ergeben null', () => {
  const g = L.standpunktMerken({ x: 1, z: -1, boden: 0, gieren: 0 }, null, ORT);
  assert.ok(L.standortAusGemerkt(g, ORT));
  assert.equal(L.standortAusGemerkt(g, 'D:\\anderes\\Projekt'), null);
  assert.equal(L.standortAusGemerkt(g, ''), null);
  assert.equal(L.standortAusGemerkt(g, undefined), null);
  assert.equal(L.standortAusGemerkt(null, ORT), null);
  assert.equal(L.standortAusGemerkt('text', ORT), null);
  assert.equal(L.standortAusGemerkt({ ...g, x: 'a' }, ORT), null);
  assert.equal(L.standortAusGemerkt({ ...g, azimutGrad: null }, ORT), null);
  assert.equal(L.standortAusGemerkt({ ...g, x: 5e9 }, ORT), null);
});

test('standortAusGemerkt: ohne Raum bleibt der Name «Letzter Standpunkt»', () => {
  const s = L.standortAusGemerkt(L.standpunktMerken({ x: 1, z: -1, boden: 0, gieren: 0 }, null, ORT), ORT);
  assert.equal(s.raum, null);
  assert.equal(s.name, 'Letzter Standpunkt');
});

test('Schlüssel der Benutzereinstellung entspricht dem Muster des Servers (Kleinbuchstaben)', () => {
  assert.match(L.SCHLUESSEL, /^[a-z][a-z0-9_.\-]{0,63}$/);
});

// ------------------------------------------------------------------------------------------ Erdgeschoss
const geschosse = [
  { kennzeichen: '1.OG', werte: { kote: 2900 } },
  { kennzeichen: 'UG', werte: { kote: -2700 } },
  { kennzeichen: 'EG', werte: { kote: 0 } },
];
const poly = { punkte: [[0, 0], [1000, 0], [1000, 1000]] };
const raum = (kz, geschoss, geometrie = poly) => ({ kennzeichen: kz, geschoss, geometrie });

test('egRaeume: Räume des untersten oberirdischen Geschosses in natürlicher Reihenfolge', () => {
  const r = [raum('R-10', 'EG'), raum('R-2', 'EG'), raum('R-1', 'UG'), raum('R-3', '1.OG'), raum('R-1', 'EG')];
  assert.deepEqual(L.egRaeume(geschosse, r), ['R-1', 'R-2', 'R-10']);
});

test('egRaeume: Räume ohne Umriss zählen nicht', () => {
  const r = [raum('R-1', 'EG', null), raum('R-2', 'EG', { punkte: [[0, 0], [1, 1]] }), raum('R-3', 'EG')];
  assert.deepEqual(L.egRaeume(geschosse, r), ['R-3']);
});

test('egRaeume: hat das unterste oberirdische Geschoss keinen Raum, gilt das nächste; Untergeschosse nie', () => {
  assert.deepEqual(L.egRaeume(geschosse, [raum('R-3', '1.OG'), raum('R-1', 'UG')]), ['R-3']);
  assert.deepEqual(L.egRaeume(geschosse, [raum('R-1', 'UG')]), []);
  assert.deepEqual(L.egRaeume([], []), []);
  assert.deepEqual(L.egRaeume(undefined, undefined), []);
});

test('egRaeume: Kote als Text und Geschoss genau auf ±0.00', () => {
  assert.deepEqual(L.egRaeume([{ kennzeichen: 'E', werte: { kote: '0' } }, { kennzeichen: 'U', werte: { kote: '-100' } }], [raum('a', 'E'), raum('b', 'U')]), ['a']);
  assert.deepEqual(L.egRaeume([{ kennzeichen: 'X', werte: {} }], [raum('a', 'X')]), []);   // ohne Kote kein «oberirdisch»
});

// ------------------------------------------------------------------------------------------ Wahl
const standortVon = (kz) => ({ raum: kz, name: kz, standpunkt: { x: 1, y: 2 }, bodenOk: 0, blick: { azimutGrad: 90 } });
const aufloeser = (gueltig, aufrufe = []) => async (bezug) => { aufrufe.push(bezug); if (!gueltig.includes(bezug)) throw new Error('unbekannt'); return standortVon(bezug); };
const gemerktStandort = L.standortAusGemerkt(L.standpunktMerken({ x: 1, z: -1, boden: 0, gieren: 0 }, null, ORT), ORT);

test('Wahl 1: der vom PC gesandte Raum geht vor allem', async () => {
  const auf = [];
  const p = await L.startpunktWaehlen({ parameter: 'R-7', gemerkt: gemerktStandort, raeumeEg: ['R-1'], raumStandort: aufloeser(['R-7', 'R-1'], auf) });
  assert.equal(p.quelle, 'parameter');
  assert.equal(p.standort.raum, 'R-7');
  assert.deepEqual(auf, ['R-7']);
});

test('Wahl 2: ein unbrauchbarer Parameter wird übergangen, dann zählt der gemerkte Standpunkt', async () => {
  const p = await L.startpunktWaehlen({ parameter: 'R-99', gemerkt: gemerktStandort, raeumeEg: ['R-1'], raumStandort: aufloeser(['R-1']) });
  assert.equal(p.quelle, 'gemerkt');
  assert.equal(p.standort, gemerktStandort);
});

test('Wahl 3: ohne Parameter und ohne gemerkten Standpunkt der erste brauchbare Raum im Erdgeschoss', async () => {
  const auf = [];
  const p = await L.startpunktWaehlen({ parameter: null, gemerkt: null, raeumeEg: ['R-1', 'R-2', 'R-3'], raumStandort: aufloeser(['R-2', 'R-3'], auf) });
  assert.equal(p.quelle, 'eg');
  assert.equal(p.standort.raum, 'R-2');
  assert.deepEqual(auf, ['R-1', 'R-2']);   // R-1 lehnt der Kern ab, R-2 nimmt er
});

test('Wahl 3: höchstens EG_VERSUCHE Räume werden versucht', async () => {
  const auf = [];
  const viele = Array.from({ length: 20 }, (_, i) => `R-${i}`);
  const p = await L.startpunktWaehlen({ raeumeEg: viele, raumStandort: aufloeser([], auf) });
  assert.equal(p.quelle, 'gesamt');
  assert.equal(auf.length, L.EG_VERSUCHE);
});

test('Wahl 4: sonst der Standpunkt der 3D-Gesamtansicht (kein Standort)', async () => {
  const p = await L.startpunktWaehlen({ raeumeEg: [], raumStandort: aufloeser([]) });
  assert.deepEqual(p, { quelle: 'gesamt', standort: null });
  assert.deepEqual(await L.startpunktWaehlen({ parameter: 'R-1', raeumeEg: [], raumStandort: async () => null }), { quelle: 'gesamt', standort: null });
});

test('Wahl: eine Antwort, die kein Objekt ist, zählt als unbrauchbar', async () => {
  const p = await L.startpunktWaehlen({ parameter: 'R-1', raeumeEg: [], raumStandort: async () => 'nix' });
  assert.equal(p.quelle, 'gesamt');
});

// ------------------------------------------------------------------------------------------ Anzeige
test('startAnzeige: Geschoss, Raum und Quelle', () => {
  const a = L.startAnzeige({ quelle: 'eg', standort: { geschoss: 'EG', geschossName: 'Erdgeschoss', nummer: '1.01', name: 'Wohnen', raum: 'R-1' } });
  assert.equal(a.geschoss, 'Erdgeschoss (EG)');
  assert.equal(a.raum, '1.01 Wohnen');
  assert.match(a.quelle, /Erdgeschoss/);
});

test('startAnzeige: ohne Raumnummer, ohne Namen und ohne Standort', () => {
  assert.equal(L.startAnzeige({ quelle: 'parameter', standort: { geschoss: 'EG', geschossName: 'EG', name: 'Bad', nummer: '' } }).geschoss, 'EG');
  assert.equal(L.startAnzeige({ quelle: 'parameter', standort: { name: '', nummer: '', raum: 'R-5' } }).raum, 'R-5');
  const g = L.startAnzeige({ quelle: 'gesamt', standort: null });
  assert.equal(g.geschoss, '');
  assert.match(g.raum, /3D-Ansicht/);
  assert.match(g.quelle, /Gesamtansicht/);
  assert.match(L.startAnzeige({ quelle: 'gemerkt', standort: { name: 'x' } }).quelle, /letzter Standpunkt/);
  assert.equal(L.startAnzeige(null).quelle, '');
});

// ------------------------------------------------------------------------------------------ Quest-Dialog (Adresse)
test('Dialog «Quest verbinden»: die anzugebende Adresse ist /vr', () => {
  assert.equal(urlText({ url: 'http://localhost:5130', port: 5130 }), 'http://localhost:5130/vr');
  assert.equal(urlText({ url: 'http://localhost:5130/', port: 5130 }), 'http://localhost:5130/vr');
  assert.equal(urlText({ port: 5680 }), 'http://localhost:5680/vr');
});

// ------------------------------------------------------------------------------------------ Aufbau der Seite
test('vr.html: ein Knopf «Eintreten», kein Menüband, Manifest verlinkt, Skripte mit absoluten Pfaden', () => {
  const html = readFileSync(new URL('../vr.html', import.meta.url), 'utf8');
  assert.match(html, /<button id="vr-eintreten"/);
  assert.match(html, />Eintreten</);
  assert.match(html, /<link rel="manifest" href="\/vr\.webmanifest">/);
  assert.match(html, /src="\/js\/vr-start\.js"/);
  assert.doesNotMatch(html, /menueband|projektbrowser|id="kopf"/);
});

test('vr.webmanifest: Start auf /vr, eigenständig, Symbole 192 und 512 als PNG', () => {
  const m = JSON.parse(readFileSync(new URL('../vr.webmanifest', import.meta.url), 'utf8'));
  assert.equal(m.name, 'KICAD VR');
  assert.equal(m.start_url, '/vr');
  assert.equal(m.display, 'standalone');
  const groessen = m.icons.map((i) => i.sizes).sort();
  assert.deepEqual(groessen, ['192x192', '512x512']);
  for (const i of m.icons) {
    assert.equal(i.type, 'image/png');
    const b = readFileSync(new URL('..' + i.src, import.meta.url));
    assert.equal(b.subarray(0, 8).toString('hex'), '89504e470d0a1a0a');
    assert.equal(b.readUInt32BE(16), Number(i.sizes.split('x')[0]));   // Breite im IHDR
  }
});

// ------------------------------------------------------------------------------------------ VR-Auftrag vom PC
test('auftragPruefen: gültige Aufträge, alles andere null', () => {
  assert.deepEqual(L.auftragPruefen({ modus: 'begehen', raum: ID, geschoss: null, nr: 3, zeit: '2026-10-21T10:00:00Z' }), { modus: 'begehen', raum: ID, geschoss: null, ausschnitt: false, ansicht: null, nr: 3, zeit: '2026-10-21T10:00:00Z' });
  assert.equal(L.auftragPruefen({ modus: 'sitzungsraum', geschoss: '1.OG', nr: 1 }).geschoss, '1.OG');
  for (const schlecht of [null, undefined, 'x', {}, { modus: 'fliegen', nr: 1 }, { modus: 'begehen', nr: 0 }, { modus: 'begehen', nr: 1.5 }, { modus: 'begehen', nr: '1' },
    { modus: 'begehen', nr: 1, raum: '../x' }, { modus: 'sitzungsraum', nr: 1, geschoss: '<b>' },
    { modus: 'sitzungsraum', nr: 1, ansicht: '../x' }, { modus: 'begehen', nr: 1, ansicht: 'AN-3D' }, { modus: 'sitzungsraum', nr: 1, ansicht: 'AN-3D', raum: 'R-1' }]) assert.equal(L.auftragPruefen(schlecht), null, JSON.stringify(schlecht));
});

test('auftragPruefen: Ausschnitt gilt nur im Sitzungsraum mit Raum, Ansicht nur im Sitzungsraum ohne Raum', () => {
  const r = L.auftragPruefen({ modus: 'sitzungsraum', raum: 'R-1', ausschnitt: true, nr: 2 });
  assert.equal(r.ausschnitt, true); assert.equal(r.raum, 'R-1'); assert.equal(r.ansicht, null);
  assert.equal(L.auftragPruefen({ modus: 'sitzungsraum', ausschnitt: true, nr: 2 }).ausschnitt, false, 'ohne Raum kein Ausschnitt');
  assert.equal(L.auftragPruefen({ modus: 'begehen', raum: 'R-1', ausschnitt: true, nr: 2 }).ausschnitt, false, 'Begehen kennt keinen Ausschnitt');
  assert.equal(L.auftragPruefen({ modus: 'sitzungsraum', raum: 'R-1', ausschnitt: 'ja', nr: 2 }).ausschnitt, false, 'nur true zählt');
  const a = L.auftragPruefen({ modus: 'sitzungsraum', ansicht: ID, nr: 4 });
  assert.equal(a.ansicht, ID); assert.equal(a.ausschnitt, false);
});

test('auftragSchritte: Raum zeigen und 3D-Ansicht, die Etage ersetzt den Ausschnitt, ein einfacher Auftrag im offenen Sitzungsraum zeigt wieder alles', () => {
  const raum = { modus: 'sitzungsraum', raum: 'R-1', ausschnitt: true, nr: 5 }, ansicht = { modus: 'sitzungsraum', ansicht: 'AN-3D', nr: 6 };
  assert.deepEqual(L.auftragSchritte(raum, { imSitzungsraum: false }), ['betreten', 'ausschnitt']);
  assert.deepEqual(L.auftragSchritte(raum, { imSitzungsraum: true }), ['ausschnitt']);
  assert.deepEqual(L.auftragSchritte(ansicht, { imSitzungsraum: false }), ['betreten', 'ausschnitt']);
  assert.deepEqual(L.auftragSchritte(ansicht, { imSitzungsraum: true }), ['ausschnitt']);
  assert.deepEqual(L.auftragSchritte({ ...raum, geschoss: '1.OG' }, { imSitzungsraum: true }), ['ausschnitt', 'etage'], 'die Etage kommt zuletzt');
  assert.deepEqual(L.auftragSchritte({ modus: 'sitzungsraum', nr: 7 }, { imSitzungsraum: true }), ['ganz']);
  assert.deepEqual(L.auftragSchritte({ modus: 'sitzungsraum', geschoss: '1.OG', nr: 7 }, { imSitzungsraum: true }), ['etage']);
  assert.deepEqual(L.auftragNachStart(raum), ['betreten', 'ausschnitt']);
  assert.deepEqual(L.auftragNachStart(ansicht), ['betreten', 'ausschnitt']);
  assert.equal(L.auftragText(raum, { geschoss: 'EG', nummer: 'E.01', name: 'Wohnen' }), 'Raum zeigen · EG · E.01 Wohnen');
  assert.equal(L.auftragText(ansicht, null, '', 'Ansicht Nord'), 'Sitzungsraum · 3D-Ansicht · Ansicht Nord');
  assert.equal(L.auftragText({ modus: 'sitzungsraum', nr: 1 }), 'Sitzungsraum');
});

test('auftragNeu: nur eine höhere Nummer als die verbrauchte ist neu', () => {
  const a = { modus: 'begehen', raum: 'R-1', nr: 5 };
  assert.equal(L.auftragNeu(a, 4), true);
  assert.equal(L.auftragNeu(a, 5), false);
  assert.equal(L.auftragNeu(a, 9), false);
  assert.equal(L.auftragNeu(a, 0), true);
  assert.equal(L.auftragNeu(a, undefined), true);
  assert.equal(L.auftragNeu(null, 0), false);
  assert.equal(L.auftragNeu({ modus: 'x', nr: 9 }, 0), false);
});

test('auftragSchritte: Begehen und Sitzungsraum, mit und ohne offenen Sitzungsraum', () => {
  const beg = { modus: 'begehen', raum: 'R-2', nr: 1 }, sitz = { modus: 'sitzungsraum', geschoss: '1.OG', nr: 2 };
  assert.deepEqual(L.auftragSchritte(beg, { imSitzungsraum: false }), ['standort']);
  assert.deepEqual(L.auftragSchritte(beg, { imSitzungsraum: true }), ['verlassen', 'standort']);
  assert.deepEqual(L.auftragSchritte(sitz, { imSitzungsraum: false }), ['betreten', 'etage']);
  assert.deepEqual(L.auftragSchritte(sitz, { imSitzungsraum: true }), ['etage']);
  assert.deepEqual(L.auftragSchritte({ modus: 'sitzungsraum', nr: 3 }, { imSitzungsraum: false }), ['betreten']);
  assert.deepEqual(L.auftragSchritte({ modus: 'sitzungsraum', nr: 3 }, { imSitzungsraum: true }), ['ganz']);
  assert.deepEqual(L.auftragSchritte({ modus: 'begehen', nr: 3 }, { imSitzungsraum: true }), ['verlassen']);
  assert.deepEqual(L.auftragSchritte({ modus: 'begehen', nr: 3 }, { imSitzungsraum: false }), []);
  assert.deepEqual(L.auftragSchritte(null), []);
  assert.deepEqual(L.auftragSchritte({ modus: 'begehen', raum: '..', nr: 1 }), []);
});

test('auftragNachStart: nur der Sitzungsraum braucht nach dem Start noch Schritte (der Standort gilt schon beim Start)', () => {
  assert.deepEqual(L.auftragNachStart({ modus: 'begehen', raum: 'R-1', nr: 1 }), []);
  assert.deepEqual(L.auftragNachStart({ modus: 'sitzungsraum', geschoss: 'EG', nr: 1 }), ['betreten', 'etage']);
  assert.deepEqual(L.auftragNachStart(null), []);
});

test('auftragText: «Begehen · EG · E.01 Wohnen/Küche» und «Sitzungsraum · 1.OG»', () => {
  const st = { raum: 'R-1', nummer: 'E.01', name: 'Wohnen/Küche', geschoss: 'EG' };
  assert.equal(L.auftragText({ modus: 'begehen', raum: 'R-1', nr: 1 }, st), 'Begehen · EG · E.01 Wohnen/Küche');
  assert.equal(L.auftragText({ modus: 'begehen', raum: 'R-1', nr: 1 }, null), 'Begehen');
  assert.equal(L.auftragText({ modus: 'sitzungsraum', geschoss: '1.OG', nr: 1 }, null, '1.OG'), 'Sitzungsraum · 1.OG');
  assert.equal(L.auftragText({ modus: 'sitzungsraum', nr: 1 }), 'Sitzungsraum');
  assert.equal(L.auftragText(null), '');
});

test('Takt des Auftrags: alle 2 s; der Auftrag steht als Quelle «auftrag» in der Anzeige', () => {
  assert.equal(L.AUFTRAG_TAKT_MS, 2000);
  assert.match(L.startAnzeige({ quelle: 'auftrag', standort: { name: 'x' } }).quelle, /Auftrag vom PC/);
});
