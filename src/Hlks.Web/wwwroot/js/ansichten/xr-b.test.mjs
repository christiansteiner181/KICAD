// Tests von Modul XR, Teile XR-B (Greifen) und XR-C (Tablet, Messwerkzeug) – der Teil ohne Brille und ohne Browser: xr-greifen-logik.js und xr-mess-logik.js
// (Greif-Erkennung aus Gelenk-Attrappen, Querverschiebung in 1-cm-Schritten, Masse zu Decke und Wand, Tablet-Geste und -Treffer, Fang und Abstand).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/xr-b.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';

const G = await import('./xr-greifen-logik.js');
const M = await import('./xr-mess-logik.js');
const nah = (a, b, eps = 1e-9, t = '') => assert.ok(Math.abs(a - b) < eps, `${t} ${a} ≠ ${b}`);
const v = (x, y, z) => ({ x, y, z });

// ------------------------------------------------------------------------------------------------ Gelenk-Attrappen
/** Linke bzw. rechte Hand mit der Handfläche nach oben (+y), Finger nach vorn (−z), Handgelenk bei ort. offen: Finger gestreckt, sonst Faust. spiegeln: Handfläche nach unten. */
function hand({ seite = 'left', offen = true, ort = v(0, 1, 0), spiegeln = false, pinch = false } = {}) {
  const s = seite === 'left' ? -1 : 1;      // Daumen- bzw. Zeigefingerseite: linke Hand mit Handfläche oben: Zeigefinger bei −x
  const sp = spiegeln ? -1 : 1;
  const p = (x, y, z) => v(ort.x + sp * x * 1, ort.y + sp * y, ort.z + z);
  const g = {
    wrist: p(0, 0, 0),
    'middle-finger-phalanx-proximal': p(0, 0, -0.09),
    'index-finger-metacarpal': p(0.02 * s, 0, -0.03),
    'pinky-finger-metacarpal': p(-0.02 * s, 0, -0.03),
    'thumb-tip': p(0.07 * s, 0, -0.06),
  };
  const tipX = { 'index-finger-tip': 0.03 * s, 'middle-finger-tip': 0.005 * s, 'ring-finger-tip': -0.02 * s, 'pinky-finger-tip': -0.04 * s };
  for (const [n, x] of Object.entries(tipX)) g[n] = offen ? p(x, 0, -0.18) : p(x * 0.5, 0.045, -0.06);   // Faust: Spitzen zur Handfläche gekrümmt
  if (pinch) g['thumb-tip'] = p(tipX['index-finger-tip'] + 0.01 * s, 0.0, offen ? -0.18 : -0.06);
  return g;
}

test('Greif-Erkennung: offene Hand ≈ 1.9 Handgrössen, Faust ≈ 1.0, mit Hysterese', () => {
  const o = hand({ offen: true }), f = hand({ offen: false });
  const vo = G.faustVerhaeltnis(o), vf = G.faustVerhaeltnis(f);
  assert.ok(vo > 1.8 && vo < 2.3, `offen ${vo}`);
  assert.ok(vf > 0.7 && vf < 1.2, `Faust ${vf}`);
  assert.equal(G.istFaust(o), false);
  assert.equal(G.istFaust(f), true);
  // Hysterese: zwischen FAUST_AN und FAUST_LOS bleibt der Zustand
  const mitte = hand({ offen: false }); for (const n of ['index-finger-tip', 'middle-finger-tip', 'ring-finger-tip', 'pinky-finger-tip']) mitte[n] = v(mitte[n].x, mitte[n].y, mitte[n].z - 0.05);
  const vm = G.faustVerhaeltnis(mitte);
  assert.ok(vm > G.FAUST_AN && vm < G.FAUST_LOS, `Mitte ${vm}`);
  assert.equal(G.istFaust(mitte, false), false, 'noch offen: greift nicht');
  assert.equal(G.istFaust(mitte, true), true, 'war gegriffen: hält');
  assert.equal(G.istFaust({}, false), false, 'unvollständige Gelenke: kein Griff');
  assert.equal(G.faustVerhaeltnis({ wrist: v(0, 0, 0) }), null);
});

test('Pinch: Daumen- und Zeigefingerspitze unter 2.5 cm, los ab 4 cm', () => {
  assert.equal(G.istPinch(hand({ offen: true }), false), false);
  assert.equal(G.istPinch(hand({ offen: true, pinch: true }), false), true);
  const g = hand({ offen: true }); g['thumb-tip'] = v(g['index-finger-tip'].x + 0.03, g['index-finger-tip'].y, g['index-finger-tip'].z);
  assert.equal(G.istPinch(g, false), false); assert.equal(G.istPinch(g, true), true, 'zwischen 2.5 und 4 cm: Hysterese');
});

// ------------------------------------------------------------------------------------------------ Reichweite
test('Reichweite: das nächste Element innerhalb 0.6 m, Leitungen vor Wänden', () => {
  const box = (x0, x1) => ({ min: v(x0, 0, 0), max: v(x1, 3, 0.2) });
  const wand = { id: 'W', box: box(0, 5), vorrang: 1 };
  const leitung = { id: 'L', strecken: [{ a: v(0, 1.5, 0.4), b: v(4, 1.5, 0.4), radius: 0.025 }], vorrang: 3 };
  const fern = { id: 'F', box: box(20, 21), vorrang: 1 };
  nah(G.abstandZuElement(v(2, 1.5, 0.5), leitung), 0.1 - 0.025, 1e-9, 'Abstand zur Rohroberfläche');
  assert.equal(G.elementInReichweite(v(2, 1.5, 0.5), [wand, leitung, fern])?.id, 'L', 'Leitung geht der Wand vor');
  assert.equal(G.elementInReichweite(v(2, 1.5, 0.15), [wand, leitung, fern])?.id, 'L');
  assert.equal(G.elementInReichweite(v(2, 1.5, 1.2), [wand, leitung, fern]), null, 'ausser Reichweite');
  assert.equal(G.elementInReichweite(v(2, 1.5, 0.9), [wand, leitung], 1.0)?.id, 'L');
  assert.equal(G.elementInReichweite(v(10, 1, 1), [wand, leitung, fern]), null);
});

// ------------------------------------------------------------------------------------------------ Querverschiebung
test('Querverschiebung: nur quer zur Achse, in 1-cm-Schritten, parallel', () => {
  const achse = v(4, 0, 0);          // waagrechte Leitung nach Osten
  const q = G.querVerschiebung(achse, v(0.5, 0.034, -0.057));   // 50 cm längs (zählt nicht), 3.4 cm hoch, 5.7 cm nach Norden
  nah(q.oben, 0.03, 1e-9, 'oben auf 1 cm gerundet'); nah(Math.abs(q.seitlich), 0.06, 1e-9, 'seitlich auf 1 cm gerundet');
  nah(q.offset.x, 0, 1e-9, 'Längsanteil entfällt'); nah(q.offset.y, 0.03, 1e-9);
  assert.ok(Math.abs(Math.abs(q.offset.z) - 0.06) < 1e-9);
  // Richtung der Achse bleibt (rein quer)
  nah(q.offset.x * 4, 0, 1e-12);
  // jeder Wert ein Vielfaches von 1 cm
  for (const d of [v(0, 0.0049, 0), v(0, 0.0051, 0), v(0, -0.0151, 0), v(0.3, 0.123, 0.456)]) {
    const r = G.querVerschiebung(achse, d);
    for (const c of [r.oben, r.seitlich]) nah(Math.round(c * 100) / 100, c, 1e-9, 'Vielfaches von 1 cm');
  }
  assert.equal(G.querVerschiebung(achse, v(0, 0.0049, 0)).oben, 0, 'unter 5 mm: kein Schritt');
  assert.equal(G.querVerschiebung(achse, v(0, 0.0051, 0)).oben, 0.01, 'ab 5 mm: ein Schritt');
  // schräge Achse: der Versatz steht senkrecht dazu
  const schraeg = v(3, 1, -2);
  const s = G.querVerschiebung(schraeg, v(0.2, 0.3, 0.1));
  nah(s.offset.x * schraeg.x + s.offset.y * schraeg.y + s.offset.z * schraeg.z, 0, 1e-9, 'senkrecht zur Achse');
  // senkrechte Leitung: zwei waagrechte Querachsen
  const hoch = G.querVerschiebung(v(0, 3, 0), v(0.1, 0.5, 0.2));
  nah(hoch.offset.y, 0, 1e-12, 'senkrechte Leitung bewegt sich nicht längs'); assert.ok(Math.abs(hoch.offset.x) > 0 || Math.abs(hoch.offset.z) > 0);
});

test('Befehlsmasse: three-Versatz → Modell-mm (x Ost, y Nord = −z, z oben)', () => {
  assert.deepEqual(G.befehlsMasse(v(0.03, 0.05, -0.06)), { dx: 30, dy: 60, dz: 50 });
  assert.deepEqual(G.befehlsMasse(v(0, 0, 0)), { dx: 0, dy: 0, dz: 0 });
  assert.deepEqual(G.befehlsMasse(v(-0.01, -0.02, 0.07)), { dx: -10, dy: -70, dz: -20 });
});

// ------------------------------------------------------------------------------------------------ Körper für Fang und Masse
/** Raum 5 × 4 m: Decke 2.8 … 3.0 m (Prisma), Wand im Westen (x = 0) und im Osten (x = 5), Modell-mm. */
const decke = { id: 'D', ebene: 'decke', teile: [{ art: 'prisma', grundriss: [[0, 0], [5000, 0], [5000, 4000], [0, 4000]], loecher: [[[4000, 1000], [4500, 1000], [4500, 1500], [4000, 1500]]], unten: 2800, oben: 3000, material: 'decke' }] };
const wandW = { id: 'WW', ebene: 'wand', teile: [{ art: 'quader', mitte: [-100, 2000, 1400], masse: [200, 4000, 2800], drehung: 0, material: 'wand' }] };
const wandO = { id: 'WO', ebene: 'wand', teile: [{ art: 'quader', mitte: [5100, 2000, 1400], masse: [200, 4000, 2800], drehung: 0, material: 'wand' }] };
const rohr = { id: 'R', ebene: 'leitung', teile: [{ art: 'zylinder', von: [500, 2000, 2500], nach: [4500, 2000, 2500], radius: 25, material: 'rohr' }] };
const raum = { id: 'RAUM', ebene: 'raum', teile: [{ art: 'quader', mitte: [2500, 2000, 1400], masse: [5000, 4000, 2800], material: 'raum' }] };
function daten(...ks) { const d = M.erzeugeFangdaten(); for (const k of ks) d.setzen(k); return d; }

test('Masse zu Decke und Wand: lotrecht nach oben, waagrecht quer zur Leitung (gegen Handrechnung)', () => {
  const d = daten(decke, wandW, wandO, rohr, raum);
  const p = M.ausModell([2500, 2000, 2500]);                        // Achse der Leitung
  const { s } = G.querAchsen(v(4, 0, 0));
  const e = M.deckeUndWaende(d, p, [s, M.mal(s, -1)], { ausser: new Set(['R']), radius: 0.025 });
  nah(e.decke.abstandMm, 300, 1e-6, 'Decke: 2800 − 2500'); nah(e.decke.abstandRandMm, 275, 1e-6, 'abzüglich Rohrradius'); assert.equal(e.decke.id, 'D');
  // zwei Wände links und rechts der Leitung (quer zur Achse x → ± z): die Wände stehen bei x = 0 und 5, also NICHT quer; die Seitenrichtung ist ± z → keine Wand
  assert.equal(e.wandA, null); assert.equal(e.wandB, null);
  // Leitung nach Norden (Achse z): quer sind ± x, die Wände bei x = 0 und x = 5 liegen links und rechts
  const q2 = G.querAchsen(v(0, 0, -4)).s;
  const e2 = M.deckeUndWaende(d, p, [q2, M.mal(q2, -1)], { ausser: new Set(['R']), radius: 0.025 });
  const werte = [e2.wandA, e2.wandB].map((w) => Math.round(w.abstandMm)).sort((a, b) => a - b);
  assert.deepEqual(werte, [2500, 2500], 'Wandinnenseiten bei x = 0 und x = 5000, Leitung in der Mitte');
  const verschoben = M.deckeUndWaende(d, M.ausModell([1000, 2000, 2650]), [q2, M.mal(q2, -1)], { ausser: new Set(['R']) });
  assert.deepEqual([verschoben.wandA, verschoben.wandB].map((w) => Math.round(w.abstandMm)).sort((a, b) => a - b), [1000, 4000]);
  nah(verschoben.decke.abstandMm, 150, 1e-6);
  // Der Raum (durchscheinendes Volumen) zählt nicht als Decke oder Wand
  assert.ok(e.decke.id !== 'RAUM');
  // unter einem Loch in der Decke gibt es keine Decke
  const loch = M.deckeUndWaende(d, M.ausModell([4250, 1250, 2500]), [], {});
  assert.equal(loch.decke, null, 'Öffnung in der Decke');
  // ohne Decke darüber
  assert.equal(M.deckeUndWaende(daten(wandW), p, [], {}).decke, null);
});

// ------------------------------------------------------------------------------------------------ Fang und Abstand
test('Fang: Anschluss vor Ecke vor Kante vor Fläche, mit Fangradius, ohne eigenes Element', () => {
  const d = daten(decke, wandW, rohr);
  // Anschluss am Rohrende (Achsenende, Ebene leitung)
  const a = M.fangenPunkt(d, M.ausModell([4510, 2005, 2500]));
  assert.equal(a.art, 'anschluss'); assert.equal(a.id, 'R'); nah(a.p.x, 4.5, 1e-9);
  // Ecke der Decke
  const e = M.fangenPunkt(d, M.ausModell([5010, -10, 3000]));
  assert.equal(e.art, 'ecke'); assert.equal(e.id, 'D'); nah(e.p.x, 5, 1e-9);
  // Achse der Leitung (mitten, aber nicht auf der Mitte)
  const k = M.fangenPunkt(d, M.ausModell([1500, 2010, 2500]));
  assert.equal(k.art, 'achse'); nah(k.p.x, 1.5, 1e-9, 'Fusspunkt auf der Achse');
  // Mitte der Achse
  assert.equal(M.fangenPunkt(d, M.ausModell([2500, 2010, 2500])).art, 'mitte');
  // Kante der Decke (Unterkante entlang y bei x = 5000)
  const kante = M.fangenPunkt(d, M.ausModell([5000, 1200, 2790]));
  assert.equal(kante.art, 'kante'); nah(kante.p.z, -1.2, 1e-9);
  // Fläche: Deckenunterseite, mit Normale nach unten
  const f = M.fangenPunkt(d, M.ausModell([2500, 2000, 2780]), { radius: 0.03 });
  assert.equal(f.art, 'flaeche'); nah(f.n.y, -1, 1e-9); nah(f.p.y, 2.8, 1e-9);
  // ausserhalb des Radius: frei
  const fr = M.fangenPunkt(d, M.ausModell([2500, 2000, 1000]));
  assert.equal(fr.art, 'frei'); nah(fr.p.y, 1, 1e-9);
  // das gegriffene Element wird nie gefangen
  assert.notEqual(M.fangenPunkt(d, M.ausModell([4510, 2005, 2500]), { ausser: new Set(['R']) }).art, 'anschluss');
});

test('Fang mit dem Strahl: Anschluss nahe am Strahl, sonst Treffer auf der Fläche', () => {
  const d = daten(decke, wandW, wandO, rohr);
  const u = M.ausModell([2500, 2000, 1500]);
  // Strahl zum Rohrende bei x = 4500 (Anschluss)
  const ziel = M.ausModell([4500, 2000, 2500]);
  const r = M.einheit(M.minus(ziel, u));
  const s = M.fangenStrahl(d, u, r);
  assert.equal(s.art, 'anschluss'); assert.equal(s.id, 'R');
  // Strahl senkrecht nach oben, abseits der Leitung: Deckenunterseite
  const oben = M.fangenStrahl(d, M.ausModell([1000, 3000, 1500]), v(0, 1, 0));
  assert.equal(oben.art, 'flaeche'); nah(oben.p.y, 2.8, 1e-9); nah(oben.t, 1.3, 1e-9);
  // Strahl nach Westen trifft die Innenseite der Wand
  const w = M.fangenStrahl(d, M.ausModell([1000, 3500, 1000]), v(-1, 0, 0));
  assert.equal(w.art, 'flaeche'); nah(w.p.x, 0, 1e-9);
  // ins Leere
  assert.equal(M.fangenStrahl(d, u, v(0, -1, 0)).art, 'frei');
});

test('Abstand: Betrag und dx/dy/dz in Modell-Richtungen, Anzeige in mm bzw. cm', () => {
  const a = M.ausModell([1000, 2000, 500]), b = M.ausModell([1300, 1600, 1700]);
  const m = M.masse(a, b);
  nah(m.dx, 300, 1e-9); nah(m.dy, -400, 1e-9); nah(m.dz, 1200, 1e-9); nah(m.abstandMm, Math.hypot(300, 400, 1200), 1e-9);
  assert.equal(M.massText(1234.4), '1234 mm · 123.4 cm');
  assert.equal(M.massText(37.26), '37.3 mm · 3.7 cm');
  assert.equal(M.massKurz(-1200), '-1200 mm', 'dx/dy/dz behalten ihr Vorzeichen');
});

test('Lot zur Fläche: Abstand zur Ebene, Fusspunkt, unabhängig von der Lage auf der Fläche', () => {
  const d = daten(decke);
  const fl = M.fangenPunkt(d, M.ausModell([3000, 1000, 2790])).flaeche;
  assert.ok(fl && Math.abs(fl.n.y + 1) < 1e-9);
  const p = M.ausModell([100, 3900, 1000]);               // weit seitlich, 1.8 m unter der Decke
  const l = M.lotMass(fl, p);
  nah(l.abstandMm, 1800, 1e-6); nah(l.fuss.x, p.x, 1e-12); nah(l.fuss.y, 2.8, 1e-9); nah(l.fuss.z, p.z, 1e-12);
});

// ------------------------------------------------------------------------------------------------ Tablet
test('Tablet-Geste: offene linke Hand, Handfläche zum Kopf; Wegdrehen und Faust schliessen', () => {
  const kopf = v(0, 1.7, 0);
  const hoch = hand({ seite: 'left', offen: true, ort: v(0, 1.1, -0.2) });
  nah(G.handflaechenNormale(hoch, 'left').y, 1, 1e-9, 'Normale der linken Hand (Handfläche oben) zeigt nach oben');
  assert.ok(G.palmWinkelZumKopf(hoch, 'left', kopf) < 25);
  assert.equal(G.tabletGeste(hoch, 'left', kopf, false), true, 'Handfläche zum Kopf: öffnet');
  const weg = hand({ seite: 'left', offen: true, ort: v(0, 1.1, -0.2), spiegeln: true });
  assert.ok(G.palmWinkelZumKopf(weg, 'left', kopf) > 150);
  assert.equal(G.tabletGeste(weg, 'left', kopf, false), false, 'weggedreht: öffnet nicht');
  assert.equal(G.tabletGeste(weg, 'left', kopf, true), false, 'weggedreht: schliesst');
  assert.equal(G.tabletGeste(hand({ seite: 'left', offen: false, ort: v(0, 1.1, -0.2) }), 'left', kopf, true), false, 'Faust schliesst');
  assert.equal(G.tabletGeste(hoch, 'left', null, false), false, 'ohne Kopf keine Geste');
  // rechte Hand spiegelbildlich: gleiche Aussage
  const r = hand({ seite: 'right', offen: true, ort: v(0.3, 1.1, -0.2) });
  nah(G.handflaechenNormale(r, 'right').y, 1, 1e-9);
  // Hysterese beim Wegdrehen: 70° neben dem Kopf – offen noch gehalten, aber nicht neu geöffnet
  const schief = hand({ seite: 'left', offen: true, ort: v(0, 1.1, -0.2) });
  const k70 = v(Math.sin(70 * Math.PI / 180) * 0.6, 1.1 + Math.cos(70 * Math.PI / 180) * 0.6, -0.2);
  assert.equal(G.tabletGeste(schief, 'left', k70, false), false); assert.equal(G.tabletGeste(schief, 'left', k70, true), true);
});

test('Tablet-Treffer: Lage über der Hand, Fingerspitze und Strahl liefern (u, v) und die Taste', () => {
  const kopf = v(0, 1.7, 1);
  const lage = G.tabletLage(v(0, 1.1, 0), v(0, 1, 0), kopf);
  nah(lage.mitte.y, 1.1 + G.TABLET_ABSTAND, 1e-9, 'über der Handfläche');
  nah(Math.hypot(lage.normale.x, lage.normale.y, lage.normale.z), 1, 1e-9);
  assert.ok(lage.normale.z > 0.5, 'dem Kopf zugewandt');
  nah(lage.rechts.x * lage.oben.x + lage.rechts.y * lage.oben.y + lage.rechts.z * lage.oben.z, 0, 1e-9, 'Rechts ⟂ Oben');
  // Die Mitte der Fläche liegt bei (0.5, 0.5)
  const k = G.tabletKoordinaten(lage, lage.mitte); nah(k.u, 0.5, 1e-9); nah(k.v, 0.5, 1e-9); assert.ok(k.innen);
  // Ein Punkt rechts oben (vom Kopf aus gesehen)
  const ro = M.plus(lage.mitte, M.plus(M.mal(lage.rechts, G.TABLET_B * 0.25), M.mal(lage.oben, G.TABLET_H * 0.25)));
  const kr = G.tabletKoordinaten(lage, ro); nah(kr.u, 0.75, 1e-9); nah(kr.v, 0.25, 1e-9);
  // Jede Taste trifft sich selbst in ihrer Mitte
  const layout = G.tabletLayout();
  assert.equal(layout.length, 13);
  for (const t of layout) assert.equal(G.tasteBei((t.x + t.w / 2) / G.TABLET_PX.b, (t.y + t.h / 2) / G.TABLET_PX.h, layout)?.id, t.id);
  assert.equal(G.tasteBei(0.5, 0.05, layout), null, 'im Kopf liegt keine Taste');
  // Fingerspitze: 1 cm vor der Fläche berührt, 10 cm davor nicht, ausserhalb nicht
  const taste = layout.find((t) => t.id === 'rueckgaengig');
  const u = (taste.x + taste.w / 2) / G.TABLET_PX.b, vv = (taste.y + taste.h / 2) / G.TABLET_PX.h;
  const stelle = (tiefe) => M.plus(lage.mitte, M.plus(M.plus(M.mal(lage.rechts, (u - 0.5) * G.TABLET_B), M.mal(lage.oben, (0.5 - vv) * G.TABLET_H)), M.mal(lage.normale, tiefe)));
  const b = G.tabletBeruehrung(lage, stelle(0.01));
  assert.ok(b); assert.equal(G.tasteBei(b.u, b.v)?.id, 'rueckgaengig');
  assert.equal(G.tabletBeruehrung(lage, stelle(0.10)), null, '10 cm davor: keine Berührung');
  assert.equal(G.tabletBeruehrung(lage, stelle(-0.08)), null, 'weit dahinter: keine Berührung');
  assert.equal(G.tabletBeruehrung(lage, M.plus(stelle(0.01), M.mal(lage.rechts, 0.4))), null, 'ausserhalb des Rechtecks');
  // Strahl vom Kopf auf die Taste
  const ziel = stelle(0), r = M.einheit(M.minus(ziel, kopf));
  const h = G.tabletStrahl(lage, kopf, r);
  assert.ok(h); nah(h.u, u, 1e-9); nah(h.v, vv, 1e-9); assert.equal(G.tasteBei(h.u, h.v)?.id, 'rueckgaengig');
  assert.equal(G.tabletStrahl(lage, kopf, M.mal(r, -1)), null, 'Strahl nach hinten trifft nicht');
});

test('Tablet-Inhalt: Platzhalter «folgt», Umschalter, Eigenschaften lesend, Aufbau und Aufleuchten', () => {
  const t = G.tabletLayout();
  for (const id of ['familie', 'problem', 'raum']) assert.equal(t.find((x) => x.id === id).platzhalter, true, id);
  for (const id of ['rueckgaengig', 'wiederholen', 'hoeher', 'tiefer', 'schnitt', 'ifc', 'messen', 'messungenLoeschen', 'lot', 'ende']) assert.ok(t.find((x) => x.id === id) && !t.find((x) => x.id === id).platzhalter, id);
  assert.deepEqual(G.eigenschaftenZeilen(null), ['Kein Element gewählt']);
  const z = G.eigenschaftenZeilen({ kennzeichen: 'RO-1', klasse: 'Rohr', system: 'KW-1', geschoss: 'EG', werte: { dn: 20, material: 'Stahl', geometrie: 'x' } });
  assert.equal(z[0], 'RO-1 · Rohr'); assert.ok(z[1].includes('System KW-1')); assert.ok(z[2].includes('dn 20') && !z[2].includes('geometrie'));
  assert.equal(G.aufbauAnteil(0), 0); assert.equal(G.aufbauAnteil(G.AUFBAU_DAUER), 1); assert.ok(G.aufbauAnteil(0.1) > 0.2 && G.aufbauAnteil(0.1) < 1.1);
  assert.equal(G.aufleuchten(0), 1); assert.equal(G.aufleuchten(G.AUFLEUCHT_DAUER), 0); assert.ok(G.aufleuchten(0.1) > 0 && G.aufleuchten(0.1) < 1);
  assert.equal(G.heranholAnteil(0), 0); assert.equal(G.heranholAnteil(G.HERANHOLEN_DAUER), 1);
});
