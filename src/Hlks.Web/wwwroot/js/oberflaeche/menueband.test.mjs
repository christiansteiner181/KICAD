// Tests der Stufenwahl des Menübands (Teil R, entwurf/menueband-revit.md) und der Verteilung der Ansichtsreiter ohne Browser:
// Aufteilung der Gruppen in Spalten je Stufe (gruppenPlan, immerGross), Stufenfolge, Stufenwahl bei viel und wenig Platz, Notfall
// «» weitere», Determinismus, Zustände des minimierten Bands, Reiterverteilung (schmaler, «▾ n weitere») und dass das CSS keinen
// Rollbalken mehr zulässt.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/menueband.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const S = await import('./menueband-stufen.js');
const R = await import('./ansichtsreiter.js');
const hier = path.dirname(fileURLToPath(import.meta.url));
const css = (name) => readFileSync(path.join(hier, '../../css', name), 'utf8');

const gross = (id, extra = {}) => ({ id, groesse: 'gross', ...extra });
const klein = (id, extra = {}) => ({ id, groesse: 'klein', ...extra });
const ids = (spalten) => spalten.map((s) => (s.art === 'gross' ? s.item.id : `[${s.stil}:${s.items.map((i) => i.id).join(',')}]`));

// Gruppe mit gegebenen Breiten je Stufe
const g = (G, M = G, K = M, Z = K) => ({ breiten: { G, M, K, Z } });
const stufenText = (r) => r.stufen.join('');

// ---------------------------------------------------------------------------------------------- Aufteilung in Spalten

test('gruppenPlan G: grosse Knöpfe einzeln, kleine (groesse klein) zu je drei in Spalten', () => {
  const liste = [gross('a'), klein('b'), klein('c'), klein('d'), klein('e'), gross('f')];
  assert.deepEqual(ids(S.gruppenPlan(liste, 'G')), ['a', '[mittel:b,c,d]', '[mittel:e]', 'f']);
});

test('gruppenPlan M: alles ausser immerGross in Spalten zu je drei, mit Text', () => {
  const liste = [gross('a'), gross('b'), gross('c'), gross('d'), gross('e')];
  assert.deepEqual(ids(S.gruppenPlan(liste, 'M')), ['[mittel:a,b,c]', '[mittel:d,e]']);
});

test('gruppenPlan K: dasselbe, aber nur Symbole', () => {
  const liste = [gross('a'), klein('b'), gross('c'), gross('d')];
  assert.deepEqual(ids(S.gruppenPlan(liste, 'K')), ['[klein:a,b,c]', '[klein:d]']);
});

test('gruppenPlan: immerGross bleibt in M und K gross und trennt die Spalten', () => {
  const liste = [gross('a'), gross('b', { immerGross: true }), gross('c'), gross('d'), gross('e'), gross('f')];
  assert.deepEqual(ids(S.gruppenPlan(liste, 'M')), ['[mittel:a]', 'b', '[mittel:c,d,e]', '[mittel:f]']);
  assert.deepEqual(ids(S.gruppenPlan(liste, 'K')), ['[klein:a]', 'b', '[klein:c,d,e]', '[klein:f]']);
  assert.deepEqual(ids(S.gruppenPlan(liste, 'G')), ['a', 'b', 'c', 'd', 'e', 'f'], 'in G ändert immerGross nichts');
});

test('gruppenPlan: eine Gruppe nur aus immerGross-Aktionen sieht in allen Stufen gleich aus', () => {
  const liste = [gross('rohr', { immerGross: true }), gross('kanal', { immerGross: true })];
  for (const stufe of ['G', 'M', 'K']) assert.deepEqual(ids(S.gruppenPlan(liste, stufe)), ['rohr', 'kanal'], stufe);
});

test('gruppenPlan: leere Gruppe, Reihenfolge bleibt erhalten, Eingabe wird nicht verändert', () => {
  assert.deepEqual(S.gruppenPlan([], 'M'), []);
  const liste = [klein('1'), gross('2'), klein('3')];
  const kopie = JSON.stringify(liste);
  const plan = S.gruppenPlan(liste, 'K');
  assert.equal(JSON.stringify(liste), kopie);
  assert.deepEqual(plan.flatMap((s) => (s.art === 'gross' ? [s.item.id] : s.items.map((i) => i.id))), ['1', '2', '3']);
});

// ---------------------------------------------------------------------------------------------- Stufenfolge

test('stufenFolge: nur Stufen, die schmaler werden', () => {
  assert.deepEqual(S.stufenFolge({ G: 150, M: 120, K: 60, Z: 50 }), ['G', 'M', 'K', 'Z']);
  assert.deepEqual(S.stufenFolge({ G: 100, M: 100, K: 60, Z: 70 }), ['G', 'K'], 'M gleich breit und Z breiter als K entfallen');
  assert.deepEqual(S.stufenFolge({ G: 80, M: 90, K: 90, Z: 60 }), ['G', 'Z'], 'eine breitere M-Stufe wird übersprungen');
  assert.deepEqual(S.stufenFolge({ G: 60, M: 60, K: 60, Z: 60 }), ['G']);
  assert.deepEqual(S.stufenFolge({ G: 60 }), ['G'], 'fehlende Breiten werden übersprungen');
});

// ---------------------------------------------------------------------------------------------- Stufenwahl

const REGISTER = [g(150, 120, 60, 50), g(150, 120, 60, 50), g(150, 120, 60, 50), g(150, 120, 60, 50)];   // vier Gruppen, 600 px gross

test('Stufenwahl bei viel Platz: alles gross, nichts ausgelagert', () => {
  const r = S.stufenWaehlen({ gruppen: REGISTER, verfuegbar: 1920, weitereBreite: 70 });
  assert.equal(stufenText(r), 'GGGG');
  assert.equal(r.weitere, 0);
  assert.equal(r.breite, 600);
});

test('Stufenwahl: genau passend bleibt gross, ein Pixel weniger verkleinert die rechteste Gruppe', () => {
  assert.equal(stufenText(S.stufenWaehlen({ gruppen: REGISTER, verfuegbar: 600 })), 'GGGG');
  const r = S.stufenWaehlen({ gruppen: REGISTER, verfuegbar: 599 });
  assert.equal(stufenText(r), 'GGGM', 'zuerst die rechteste Gruppe, eine Stufe');
  assert.equal(r.breite, 570);
});

test('Stufenwahl: von rechts nach links, jede Gruppe erst eine Stufe, dann die nächste', () => {
  const erwartung = [[600, 'GGGG'], [570, 'GGGM'], [540, 'GGMM'], [510, 'GMMM'], [480, 'MMMM'], [420, 'MMMK'], [360, 'MMKK'], [300, 'MKKK'], [240, 'KKKK'], [230, 'KKKZ'], [220, 'KKZZ'], [210, 'KZZZ'], [200, 'ZZZZ']];
  for (const [platz, text] of erwartung) assert.equal(stufenText(S.stufenWaehlen({ gruppen: REGISTER, verfuegbar: platz })), text, `Platz ${platz}`);
});

test('Stufenwahl: die erste Gruppe wird zuletzt verkleinert', () => {
  for (let platz = 600; platz >= 120; platz--) {
    const t = stufenText(S.stufenWaehlen({ gruppen: REGISTER, verfuegbar: platz }));
    const rang = [...t].map((c) => 'GMKZW'.indexOf(c));
    for (let i = 0; i + 1 < rang.length; i++) assert.ok(rang[i] <= rang[i + 1], `Platz ${platz}: ${t} – links nie kleiner als rechts`);
  }
});

test('Stufenwahl ist monoton: mehr Platz nie eine kleinere Stufe', () => {
  const rangSumme = (r) => r.stufen.reduce((s, c) => s + 'GMKZW'.indexOf(c), 0);
  let vorher = Infinity;
  for (let platz = 1; platz <= 700; platz += 1) {
    const s = rangSumme(S.stufenWaehlen({ gruppen: REGISTER, verfuegbar: platz, weitereBreite: 70 }));
    assert.ok(s <= vorher, `Platz ${platz}: ${s} > ${vorher}`);
    vorher = s;
  }
});

test('Stufenwahl: das Ergebnis passt immer in den Platz (ausser beim Notfall mit nur noch «weitere»)', () => {
  for (let platz = 70; platz <= 700; platz++) {
    const r = S.stufenWaehlen({ gruppen: REGISTER, verfuegbar: platz, weitereBreite: 70 });
    assert.ok(r.breite <= platz, `Platz ${platz}: belegt ${r.breite}`);
  }
});

test('Stufenwahl: Stufen ohne Gewinn werden übersprungen (Name breiter als die Symbole)', () => {
  // M gleich breit wie G, K und Z gleich breit: die Gruppe hat nur G und K
  const gruppen = [g(100, 100, 70, 70), g(100, 100, 70, 70)];
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 199 })), 'GK');
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 140 })), 'KK');
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 135, weitereBreite: 60 })), 'KW', 'Z gibt es nicht, also wandert die rechte Gruppe nach «weitere»');
});

test('Stufenwahl mit immerGross: eine Gruppe, die nur aus immerGross besteht, geht von G gleich auf Z', () => {
  // Breiten aus dem Plan abgeleitet wie die Messung: gross 60, mittel-Spalte 90, klein-Spalte 24, zugeklappt 50
  const breite = (liste, stufe) => (stufe === 'Z' ? 50 : S.gruppenPlan(liste, stufe).reduce((s, sp) => s + (sp.art === 'gross' ? 60 : sp.stil === 'mittel' ? 90 : 24), 0));
  const messen = (liste) => ({ breiten: Object.fromEntries(S.STUFEN.map((s) => [s, breite(liste, s)])) });
  const leitung = [gross('rohr', { immerGross: true }), gross('kanal', { immerGross: true })];
  const bauteile = [gross('a'), gross('b'), gross('c')];
  assert.deepEqual(messen(leitung).breiten, { G: 120, M: 120, K: 120, Z: 50 });
  assert.deepEqual(S.stufenFolge(messen(leitung).breiten), ['G', 'Z'], 'immerGross: M und K sind nicht schmaler');
  assert.deepEqual(messen(bauteile).breiten, { G: 180, M: 90, K: 24, Z: 50 });
  assert.deepEqual(S.stufenFolge(messen(bauteile).breiten), ['G', 'M', 'K'], 'Z (50) ist breiter als K (24) und entfällt');
  const gruppen = [messen(bauteile), messen(leitung)];   // links Bauteile (180), rechts Leitung (120): zusammen 300
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 300 })), 'GG');
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 299 })), 'MG', 'die immerGross-Gruppe bleibt gross, die Bauteile werden mittel');
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 209 })), 'KG');
  const r = S.stufenWaehlen({ gruppen, verfuegbar: 143 });
  assert.equal(stufenText(r), 'KZ', 'erst zuletzt klappt die immerGross-Gruppe zu – sie war bis dahin gross');
  assert.equal(r.breite, 74);
});

test('Stufenwahl: eine Gruppe, deren M nichts bringt, springt nicht über eine Stufe, sondern wartet auf den nächsten Durchgang', () => {
  const gruppen = [g(200, 150, 100, 60), g(100, 100, 70, 70)];   // rechts: nur G und K
  assert.deepEqual(S.stufenFolge(gruppen[1].breiten), ['G', 'K']);
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 299 })), 'MG', 'Durchgang 1 (M): rechts gibt es kein schmaleres M, links wird M');
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 249 })), 'MK', 'Durchgang 2 (K): erst jetzt wird die rechte Gruppe klein');
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 170 })), 'KK');
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 169 })), 'ZK', 'Durchgang 3 (Z): links klappt zu, rechts gibt es kein Z');
});

test('Stufenwahl: Notfall – reicht alles auf der letzten Stufe nicht, wandern Gruppen von rechts nach «weitere»', () => {
  const r = S.stufenWaehlen({ gruppen: REGISTER, verfuegbar: 119, weitereBreite: 70 });
  // ZZZZ = 200 passt nicht; mit Z-Breite 50: weitere=1 → 3*50+70 = 220, =2 → 170, =3 → 120, =4 → 70
  assert.equal(r.weitere, 4);
  assert.equal(stufenText(r), 'WWWW');
  const r2 = S.stufenWaehlen({ gruppen: REGISTER, verfuegbar: 180, weitereBreite: 70 });
  assert.equal(stufenText(r2), 'ZZWW', 'zwei Gruppen bleiben, zwei wandern');
  assert.equal(r2.weitere, 2);
  assert.ok(r2.breite <= 180);
});

test('Stufenwahl: Notfall bei nur einer Gruppe und zu wenig Platz – die Gruppe steht dann im Flyout', () => {
  const r = S.stufenWaehlen({ gruppen: [g(150, 120, 60, 50)], verfuegbar: 40, weitereBreite: 70 });
  assert.equal(stufenText(r), 'W');
  assert.equal(r.weitere, 1);
});

test('Stufenwahl: ohne bekannte Breite wird nichts verkleinert', () => {
  for (const v of [0, -5, NaN, undefined]) assert.equal(stufenText(S.stufenWaehlen({ gruppen: REGISTER, verfuegbar: v })), 'GGGG');
});

test('Stufenwahl ohne Gruppen', () => {
  assert.deepEqual(S.stufenWaehlen({ gruppen: [], verfuegbar: 100 }), { stufen: [], weitere: 0, breite: 0 });
});

test('Stufenwahl ist deterministisch und verändert die Eingabe nicht', () => {
  const eingabe = { gruppen: REGISTER, verfuegbar: 333, weitereBreite: 70 };
  const kopie = JSON.stringify(eingabe);
  const a = S.stufenWaehlen(eingabe), b = S.stufenWaehlen(eingabe), c = S.stufenWaehlen(JSON.parse(kopie));
  assert.deepEqual(a, b);
  assert.deepEqual(a, c);
  assert.equal(JSON.stringify(eingabe), kopie);
});

test('Stufenwahl mit unterschiedlich breiten Gruppen: rechts wird zuerst gespart, auch wenn links mehr zu holen wäre', () => {
  const gruppen = [g(300, 200, 100, 60), g(60, 50, 40, 40), g(60, 50, 40, 40)];
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 419 })), 'GGM');
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 409 })), 'GMM');
  assert.equal(stufenText(S.stufenWaehlen({ gruppen, verfuegbar: 399 })), 'MMM');
});

// ---------------------------------------------------------------------------------------------- minimiertes Band

test('minWeiter: voll → Gruppentitel → Register → voll', () => {
  assert.equal(S.minWeiter('voll'), 'titel');
  assert.equal(S.minWeiter('titel'), 'register');
  assert.equal(S.minWeiter('register'), 'voll');
  assert.equal(S.minWeiter('unsinn'), 'titel');
  assert.deepEqual(S.MIN_ZUSTAENDE, ['voll', 'titel', 'register']);
});

test('minUmschalten (Doppelklick): voll ↔ zuletzt gewählter minimierter Zustand, Vorgabe «register»', () => {
  assert.deepEqual(S.minUmschalten('voll', undefined), { zustand: 'register', letzte: 'register' });
  assert.deepEqual(S.minUmschalten('voll', 'titel'), { zustand: 'titel', letzte: 'titel' });
  assert.deepEqual(S.minUmschalten('titel', 'register'), { zustand: 'voll', letzte: 'titel' });
  assert.deepEqual(S.minUmschalten('register', 'titel'), { zustand: 'voll', letzte: 'register' });
  assert.deepEqual(S.minUmschalten('voll', 'unsinn'), { zustand: 'register', letzte: 'register' });
});

// ---------------------------------------------------------------------------------------------- Ansichtsreiter

const breitenSumme = (r) => r.sichtbar.reduce((s, i) => s + r.breiten[i], 0) + Math.max(0, r.sichtbar.length - 1) * R.ABSTAND;

test('Reiter: passen alle, bleiben sie natürlich breit', () => {
  const r = R.reiterPlan({ breiten: [120, 140, 100], aktiv: 0, verfuegbar: 1000, weitereBreite: 80 });
  assert.deepEqual(r.sichtbar, [0, 1, 2]);
  assert.deepEqual(r.versteckt, []);
  assert.deepEqual(r.breiten, { 0: 120, 1: 140, 2: 100 });
});

test('Reiter: knapp → längere werden schmaler, kurze bleiben, nichts wird ausgeblendet', () => {
  const r = R.reiterPlan({ breiten: [200, 100, 200], aktiv: 0, verfuegbar: 400 });
  assert.deepEqual(r.versteckt, []);
  assert.equal(r.breiten[1], 100, 'der kurze Reiter bleibt');
  assert.ok(r.breiten[0] < 200 && r.breiten[0] === r.breiten[2], 'die langen teilen sich den Rest');
  assert.ok(breitenSumme(r) <= 400, `Summe ${breitenSumme(r)}`);
  assert.ok(Object.values(r.breiten).every((b) => b >= R.MINDESTBREITE));
});

test('Reiter: nie schmaler als die Mindestbreite; erst danach «weitere»', () => {
  const breiten = Array(10).fill(140);
  const eng = R.reiterPlan({ breiten, aktiv: 0, verfuegbar: R.MINDESTBREITE * 10 + 9, weitereBreite: 80 });
  assert.equal(eng.versteckt.length, 0, 'bei genau Mindestbreite × 10 passen alle noch');
  assert.ok(Object.values(eng.breiten).every((b) => b === R.MINDESTBREITE));
  const enger = R.reiterPlan({ breiten, aktiv: 0, verfuegbar: R.MINDESTBREITE * 10 + 8, weitereBreite: 80 });
  assert.ok(enger.versteckt.length > 0, 'ein Pixel weniger: «weitere»');
  assert.ok(enger.sichtbar.length + enger.versteckt.length === 10);
  assert.ok(breitenSumme(enger) + 80 + R.ABSTAND <= R.MINDESTBREITE * 10 + 8, 'die sichtbaren Reiter und der Knopf passen');
});

test('Reiter: der aktive Reiter bleibt sichtbar, auch wenn er hinten steht', () => {
  const breiten = Array(10).fill(140);
  const r = R.reiterPlan({ breiten, aktiv: 9, verfuegbar: 400, weitereBreite: 80 });
  assert.ok(r.sichtbar.includes(9));
  assert.ok(r.versteckt.length > 0);
  assert.deepEqual([...r.sichtbar].sort((a, b) => a - b), r.sichtbar, 'Reihenfolge der Ansichten bleibt');
  const vorn = R.reiterPlan({ breiten, aktiv: 0, verfuegbar: 400, weitereBreite: 80 });
  assert.deepEqual(vorn.sichtbar, vorn.sichtbar.slice().sort((a, b) => a - b));
  assert.equal(vorn.sichtbar[0], 0);
});

test('Reiter: auch bei sehr wenig Platz bleibt mindestens der aktive Reiter', () => {
  const r = R.reiterPlan({ breiten: [140, 140, 140], aktiv: 2, verfuegbar: 120, weitereBreite: 80 });
  assert.deepEqual(r.sichtbar, [2]);
  assert.deepEqual(r.versteckt, [0, 1]);
});

test('Reiter: kein Reiter, kein Platz bekannt, deterministisch', () => {
  assert.deepEqual(R.reiterPlan({ breiten: [], aktiv: -1, verfuegbar: 100 }), { sichtbar: [], versteckt: [], breiten: {} });
  assert.deepEqual(R.reiterPlan({ breiten: [150, 150], aktiv: 0, verfuegbar: 0 }).versteckt, []);
  const e = { breiten: [180, 120, 150, 90, 200, 170], aktiv: 3, verfuegbar: 520, weitereBreite: 84 };
  assert.deepEqual(R.reiterPlan(e), R.reiterPlan(JSON.parse(JSON.stringify(e))));
});

test('Reiter: weitereText nennt die Anzahl', () => {
  assert.equal(R.weitereText(3), '▾ 3 weitere');
});

// ---------------------------------------------------------------------------------------------- CSS: kein Rollbalken

test('CSS: Menüband-Feld und Ansichtsreiter haben keinen Rollbalken mehr', () => {
  const ob = css('oberflaeche.css'), hl = css('hlks.css');
  const regel = (text, selektor) => { const m = text.match(new RegExp('(^|\\n)' + selektor.replace(/[.*+?^${}()|[\]\\]/g, '\\$&') + '\\s*\\{([^}]*)\\}')); return m ? m[2] : ''; };
  assert.doesNotMatch(regel(ob, '.mb-feld'), /overflow-x\s*:\s*(auto|scroll)/);
  assert.match(regel(ob, '.mb-feld'), /overflow\s*:\s*hidden/);
  assert.doesNotMatch(regel(hl, '#ansichtsreiter'), /overflow(-x)?\s*:\s*(auto|scroll)/);
  const an = css('anordnung.css');   // seit W2 hat jede Gruppe ihre Reiterleiste
  assert.match(regel(an, '.reiterleiste'), /position\s*:\s*absolute/);
  assert.doesNotMatch(regel(an, '.reiterleiste'), /overflow(-x)?\s*:\s*(auto|scroll)/);
  assert.doesNotMatch(regel(ob, '#optionsleiste'), /nowrap/);
  assert.match(regel(ob, '#optionsleiste'), /flex-wrap\s*:\s*wrap/);
});
