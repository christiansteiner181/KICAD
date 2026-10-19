// Tests der Anordnung der Ansichten (Teil W2, entwurf/fenster-revit.md) ohne Browser: Baum aus Gruppen und Teilungen – hinzufügen, entfernen,
// verschieben (teilen, zusammenlegen), Trenner, Kacheln, Merken und Laden (auch ungültige Werte), Rechtecke, Andockziele; dazu die
// reinen Teile des Zweitfensters (Adresse, Nummer, Optionen) und dass die Dateien zusammenpassen (CSS, Einbindung).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/anordnung.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const A = await import('./anordnung.js');
const Z = await import('./zweitfenster.js');
const hier = path.dirname(fileURLToPath(import.meta.url));

const aus = (...namen) => namen.reduce((a, n) => A.hinzufuegen(a, n), A.leer());
const summe = (xs) => xs.reduce((s, x) => s + x, 0);
const nahe = (a, b, eps = 1e-9) => assert.ok(Math.abs(a - b) < eps, `${a} ≠ ${b}`);

/** Prüft die Zusicherungen einer sauberen Anordnung. */
function sauber(a) {
  const namen = A.ansichten(a);
  assert.equal(new Set(namen).size, namen.length, 'jede Ansicht höchstens einmal');
  const ids = A.gruppen(a).map((g) => g.id);
  assert.equal(new Set(ids).size, ids.length, 'Gruppen-Ids eindeutig');
  for (const g of A.gruppen(a)) { assert.ok(g.ansichten.length > 0, 'keine leere Gruppe'); assert.ok(g.ansichten.includes(g.aktiv), 'aktiv gültig'); }
  const geh = (k) => {
    if (k.art !== 'teilung') return;
    assert.ok(k.kinder.length >= 2, 'Teilung hat mindestens zwei Kinder');
    assert.equal(k.anteile.length, k.kinder.length);
    nahe(summe(k.anteile), 1);
    for (const x of k.anteile) assert.ok(x > 0);
    for (const c of k.kinder) { assert.ok(!(c.art === 'teilung' && c.richtung === k.richtung), 'keine gleichrichtige Teilung in einer Teilung'); geh(c); }
  };
  if (a.wurzel) geh(a.wurzel);
  if (a.wurzel) assert.ok(ids.includes(a.aktiveGruppe), 'aktiveGruppe gültig'); else assert.equal(a.aktiveGruppe, null);
  for (const x of a.ausgelagert) assert.ok(!namen.includes(x), 'ausgelagert nicht zugleich im Baum');
  return a;
}

// ---------------------------------------------------------------------------------------------- hinzufügen, entfernen, aktivieren

test('leer: keine Gruppe, keine aktive Ansicht, Modus «reiter»', () => {
  const a = sauber(A.leer());
  assert.deepEqual(A.gruppen(a), []);
  assert.equal(A.aktiveAnsicht(a), null);
  assert.equal(A.modus(a), 'reiter');
});

test('hinzufügen: erste Ansicht legt eine Gruppe an, weitere werden Reiter der aktiven Gruppe und aktiv', () => {
  const a = sauber(aus('a', 'b', 'c'));
  assert.equal(A.gruppen(a).length, 1);
  assert.deepEqual(A.gruppen(a)[0].ansichten, ['a', 'b', 'c']);
  assert.equal(A.aktiveAnsicht(a), 'c');
  assert.equal(A.modus(a), 'reiter');
});

test('hinzufügen: schon vorhandene Ansicht wird nur aktiv (kein Doppel)', () => {
  const a = sauber(A.hinzufuegen(aus('a', 'b'), 'a'));
  assert.deepEqual(A.ansichten(a), ['a', 'b']);
  assert.equal(A.aktiveAnsicht(a), 'a');
});

test('hinzufügen: in eine bestimmte Gruppe an eine bestimmte Stelle', () => {
  let a = aus('a', 'b');
  a = A.verschieben(a, 'b', { gruppe: 'g1', ort: 'rechts' });
  const links = A.gruppeVon(a, 'a').id, rechts = A.gruppeVon(a, 'b').id;
  a = sauber(A.hinzufuegen(a, 'c', { gruppe: links, index: 0 }));
  assert.deepEqual(A.findeGruppe(a, links).ansichten, ['c', 'a']);
  assert.deepEqual(A.findeGruppe(a, rechts).ansichten, ['b']);
  assert.equal(A.aktiveGruppe(a).id, links);
});

test('entfernen: die aktive Ansicht geht, die letzte der Gruppe wird aktiv (wie die bisherigen Reiter)', () => {
  let a = aus('a', 'b', 'c');
  a = A.aktivieren(a, 'b');
  a = sauber(A.entfernen(a, 'b'));
  assert.deepEqual(A.ansichten(a), ['a', 'c']);
  assert.equal(A.aktiveAnsicht(a), 'c');
  a = sauber(A.entfernen(a, 'c'));
  assert.equal(A.aktiveAnsicht(a), 'a');
  a = sauber(A.entfernen(a, 'a'));
  assert.equal(a.wurzel, null);
  assert.equal(A.aktiveAnsicht(a), null);
});

test('entfernen: unbekannte Ansicht ändert nichts', () => {
  const a = aus('a', 'b');
  assert.deepEqual(A.entfernen(a, 'x'), a);
});

test('entfernen: leere Gruppe verschwindet, die Teilung löst sich auf, die Nachbargruppe wird aktiv', () => {
  let a = A.verschieben(aus('a', 'b'), 'b', { gruppe: 'g1', ort: 'rechts' });
  assert.equal(A.gruppen(a).length, 2);
  a = sauber(A.entfernen(a, 'b'));
  assert.equal(A.gruppen(a).length, 1);
  assert.equal(a.wurzel.art, 'gruppe');
  assert.equal(A.aktiveAnsicht(a), 'a');
});

test('aktivieren: setzt Reiter und Gruppe', () => {
  let a = A.verschieben(aus('a', 'b', 'c'), 'c', { gruppe: 'g1', ort: 'rechts' });
  a = A.aktivieren(a, 'a');
  assert.equal(A.aktiveAnsicht(a), 'a');
  assert.equal(A.aktiveGruppe(a).id, A.gruppeVon(a, 'a').id);
  assert.equal(A.aktiveAnsicht(A.aktivieren(a, 'gibtsnicht')), 'a');
});

// ---------------------------------------------------------------------------------------------- verschieben: teilen und zusammenlegen

test('verschieben an den rechten Rand der Gruppe: Teilung nebeneinander, je halber Platz, die Ansicht ist aktiv', () => {
  const a = sauber(A.verschieben(aus('a', 'b'), 'b', { gruppe: 'g1', ort: 'rechts' }));
  assert.equal(a.wurzel.art, 'teilung');
  assert.equal(a.wurzel.richtung, 'nebeneinander');
  assert.deepEqual(a.wurzel.anteile, [0.5, 0.5]);
  assert.deepEqual(A.gruppen(a).map((g) => g.ansichten), [['a'], ['b']]);
  assert.equal(A.aktiveAnsicht(a), 'b');
  assert.equal(A.modus(a), 'nebeneinander');
});

test('verschieben nach links und oben stellt die neue Gruppe davor; unten und oben ergeben «untereinander»', () => {
  const l = sauber(A.verschieben(aus('a', 'b'), 'b', { gruppe: 'g1', ort: 'links' }));
  assert.deepEqual(A.gruppen(l).map((g) => g.ansichten), [['b'], ['a']]);
  const o = sauber(A.verschieben(aus('a', 'b'), 'b', { gruppe: 'g1', ort: 'oben' }));
  assert.equal(o.wurzel.richtung, 'untereinander');
  assert.deepEqual(A.gruppen(o).map((g) => g.ansichten), [['b'], ['a']]);
  const u = sauber(A.verschieben(aus('a', 'b'), 'b', { gruppe: 'g1', ort: 'unten' }));
  assert.deepEqual(A.gruppen(u).map((g) => g.ansichten), [['a'], ['b']]);
});

test('drei Gruppen: weiteres Teilen in gleicher Richtung hängt in dieselbe Teilung und halbiert nur den Anteil der geteilten Gruppe', () => {
  let a = A.verschieben(aus('a', 'b', 'c'), 'b', { gruppe: 'g1', ort: 'rechts' });   // [a c | b]
  const ga = A.gruppeVon(a, 'a').id;
  a = sauber(A.verschieben(a, 'c', { gruppe: ga, ort: 'rechts' }));                   // [a | c | b]
  assert.equal(a.wurzel.kinder.length, 3);
  assert.deepEqual(A.gruppen(a).map((g) => g.ansichten), [['a'], ['c'], ['b']]);
  assert.deepEqual(a.wurzel.anteile, [0.25, 0.25, 0.5]);
});

test('Teilen in der anderen Richtung verschachtelt: links eine Gruppe, rechts zwei untereinander', () => {
  let a = A.verschieben(aus('a', 'b', 'c'), 'b', { gruppe: 'g1', ort: 'rechts' });
  const gb = A.gruppeVon(a, 'b').id;
  a = A.hinzufuegen(a, 'c', { gruppe: gb });
  a = sauber(A.verschieben(a, 'c', { gruppe: gb, ort: 'unten' }));
  assert.equal(a.wurzel.richtung, 'nebeneinander');
  assert.equal(a.wurzel.kinder[1].art, 'teilung');
  assert.equal(a.wurzel.kinder[1].richtung, 'untereinander');
  assert.deepEqual(A.gruppen(a).map((g) => g.ansichten), [['a'], ['b'], ['c']]);
});

test('verschieben «mitte» in eine andere Gruppe legt als Reiter zusammen; die leere Quellgruppe verschwindet', () => {
  let a = A.verschieben(aus('a', 'b'), 'b', { gruppe: 'g1', ort: 'rechts' });
  const ga = A.gruppeVon(a, 'a').id;
  a = sauber(A.verschieben(a, 'b', { gruppe: ga, ort: 'mitte' }));
  assert.equal(a.wurzel.art, 'gruppe');
  assert.deepEqual(a.wurzel.ansichten, ['a', 'b']);
  assert.equal(A.aktiveAnsicht(a), 'b');
});

test('verschieben «mitte» mit Index sortiert Reiter innerhalb der Gruppe um (Index zählt in der Liste vor dem Herausnehmen)', () => {
  const a0 = aus('a', 'b', 'c');
  assert.deepEqual(A.ansichten(sauber(A.verschieben(a0, 'a', { gruppe: 'g1', ort: 'mitte', index: 3 }))), ['b', 'c', 'a']);
  assert.deepEqual(A.ansichten(sauber(A.verschieben(a0, 'c', { gruppe: 'g1', ort: 'mitte', index: 0 }))), ['c', 'a', 'b']);
  assert.deepEqual(A.ansichten(sauber(A.verschieben(a0, 'b', { gruppe: 'g1', ort: 'mitte', index: 1 }))), ['a', 'b', 'c']);
});

test('die einzige Ansicht einer Gruppe lässt sich nicht an die eigene Gruppe andocken (keine Änderung ausser aktiv)', () => {
  const a0 = A.verschieben(aus('a', 'b'), 'b', { gruppe: 'g1', ort: 'rechts' });
  const gb = A.gruppeVon(a0, 'b').id;
  for (const ort of ['links', 'rechts', 'oben', 'unten', 'mitte']) {
    const a = sauber(A.verschieben(a0, 'b', { gruppe: gb, ort }));
    assert.deepEqual(A.gruppen(a).map((g) => g.ansichten), [['a'], ['b']], ort);
  }
});

test('verschieben an den Rand der ganzen Fläche: neue Gruppe mit 30 %, bestehende Teilung gleicher Richtung wird erweitert', () => {
  let a = sauber(A.verschieben(aus('a', 'b'), 'b', { rand: 'links' }));
  assert.deepEqual(A.gruppen(a).map((g) => g.ansichten), [['b'], ['a']]);
  nahe(a.wurzel.anteile[0], 0.3);
  a = A.hinzufuegen(a, 'c');
  a = sauber(A.verschieben(a, 'c', { rand: 'rechts' }));
  assert.equal(a.wurzel.kinder.length, 3);
  nahe(a.wurzel.anteile[2], 0.3);
  const u = sauber(A.verschieben(a, 'a', { rand: 'unten' }));
  assert.equal(u.wurzel.richtung, 'untereinander');
  assert.equal(u.wurzel.kinder.length, 2);
});

test('verschieben einer ausgelagerten oder neuen Ansicht fügt sie ein; ungültige Ziele ändern nichts', () => {
  let a = A.auslagern(aus('a', 'b'), 'b');
  assert.deepEqual(a.ausgelagert, ['b']);
  a = sauber(A.verschieben(a, 'b', { gruppe: 'g1', ort: 'rechts' }));
  assert.deepEqual(a.ausgelagert, []);
  assert.deepEqual(A.ansichten(a), ['a', 'b']);
  const b0 = aus('a', 'b');
  assert.deepEqual(A.ansichten(A.verschieben(b0, 'a', { gruppe: 'gibtsnicht', ort: 'rechts' })), ['a', 'b']);
  assert.deepEqual(A.ansichten(A.verschieben(b0, 'a', { gruppe: 'g1', ort: 'quer' })), ['a', 'b']);
  assert.deepEqual(A.verschieben(b0, 'a', { rand: 'mitte' }).wurzel.ansichten, ['a', 'b']);
});

test('Eingabe wird nie verändert (reine Funktionen)', () => {
  const a = aus('a', 'b', 'c');
  const vorher = JSON.stringify(a);
  A.verschieben(a, 'c', { gruppe: 'g1', ort: 'rechts' }); A.entfernen(a, 'a'); A.kacheln(a); A.zusammenlegen(a); A.auslagern(a, 'a'); A.aktivieren(a, 'a');
  assert.equal(JSON.stringify(a), vorher);
});

// ---------------------------------------------------------------------------------------------- Trenner

test('trennerSetzen verschiebt nur die beiden Nachbarn und hält die Summe', () => {
  let a = A.verschieben(aus('a', 'b', 'c'), 'b', { gruppe: 'g1', ort: 'rechts' });
  a = A.verschieben(a, 'c', { gruppe: A.gruppeVon(a, 'a').id, ort: 'rechts' });    // [a | c | b] = .25 .25 .5
  const t = sauber(A.trennerSetzen(a, [], 0, 0.1, 0.05));
  nahe(t.wurzel.anteile[0], 0.1); nahe(t.wurzel.anteile[1], 0.4); nahe(t.wurzel.anteile[2], 0.5);
  const u = sauber(A.trennerSetzen(a, [], 1, 0.6, 0.05));
  nahe(u.wurzel.anteile[0], 0.25); nahe(u.wurzel.anteile[1], 0.35); nahe(u.wurzel.anteile[2], 0.4);
});

test('trennerSetzen hält den Mindestanteil beider Nachbarn und ignoriert Unsinn', () => {
  const a = A.verschieben(aus('a', 'b'), 'b', { gruppe: 'g1', ort: 'rechts' });
  const links = A.trennerSetzen(a, [], 0, -5, 0.2);
  nahe(links.wurzel.anteile[0], 0.2); nahe(links.wurzel.anteile[1], 0.8);
  const rechts = A.trennerSetzen(a, [], 0, 7, 0.2);
  nahe(rechts.wurzel.anteile[0], 0.8); nahe(rechts.wurzel.anteile[1], 0.2);
  assert.deepEqual(A.trennerSetzen(a, [], 5, 0.3), a);          // Index ausserhalb
  assert.deepEqual(A.trennerSetzen(a, [3], 0, 0.3), a);         // Pfad ins Leere
  assert.deepEqual(A.trennerSetzen(a, [], 0, NaN), a);
  const riesig = A.trennerSetzen(a, [], 0, 0.5, 0.9);           // Mindestanteil grösser als möglich: höchstens die Hälfte
  nahe(riesig.wurzel.anteile[0], 0.5);
});

test('trennerSetzen in einer verschachtelten Teilung über den Pfad', () => {
  let a = A.verschieben(aus('a', 'b', 'c'), 'b', { gruppe: 'g1', ort: 'rechts' });
  const gb = A.gruppeVon(a, 'b').id;
  a = A.verschieben(A.hinzufuegen(a, 'c', { gruppe: gb }), 'c', { gruppe: gb, ort: 'unten' });
  const t = sauber(A.trennerSetzen(a, [1], 0, 0.3, 0.1));
  nahe(t.wurzel.kinder[1].anteile[0], 0.3); nahe(t.wurzel.kinder[1].anteile[1], 0.7);
  assert.deepEqual(t.wurzel.anteile, a.wurzel.anteile);
});

// ---------------------------------------------------------------------------------------------- Kacheln und Zusammenlegen

test('Kacheln: n Ansichten gleichmässig, Spalten = ⌈√n⌉, jede Ansicht eine Gruppe', () => {
  const k4 = sauber(A.kacheln(aus('a', 'b', 'c', 'd')));
  assert.deepEqual(A.gruppen(k4).map((g) => g.ansichten), [['a'], ['b'], ['c'], ['d']]);
  assert.equal(k4.wurzel.richtung, 'untereinander');
  assert.equal(k4.wurzel.kinder.length, 2);
  assert.ok(k4.wurzel.kinder.every((r) => r.richtung === 'nebeneinander' && r.kinder.length === 2));
  const k3 = sauber(A.kacheln(aus('a', 'b', 'c')));
  assert.equal(k3.wurzel.kinder.length, 2);
  assert.equal(k3.wurzel.kinder[1].art, 'gruppe', 'die letzte Reihe mit einer Ansicht füllt die Breite');
  const k2 = sauber(A.kacheln(aus('a', 'b')));
  assert.equal(k2.wurzel.richtung, 'nebeneinander');
  assert.equal(A.modus(k2), 'nebeneinander');
  const k9 = sauber(A.kacheln(aus(...'abcdefghi')));
  assert.equal(A.gruppen(k9).length, 9);
  assert.equal(k9.wurzel.kinder.length, 3);
});

test('Kacheln: bleibt bei einer Ansicht eine Gruppe, die aktive Ansicht bleibt aktiv, ausgelagerte bleiben ausgelagert', () => {
  assert.equal(sauber(A.kacheln(aus('a'))).wurzel.art, 'gruppe');
  let a = A.auslagern(aus('a', 'b', 'c'), 'c');
  a = A.aktivieren(a, 'a');
  const k = sauber(A.kacheln(a));
  assert.equal(A.aktiveAnsicht(k), 'a');
  assert.deepEqual(k.ausgelagert, ['c']);
  assert.equal(sauber(A.kacheln(A.leer())).wurzel, null);
});

test('Kacheln behält die Reihenfolge von Gruppen und Reitern (links nach rechts, oben nach unten)', () => {
  let a = A.verschieben(aus('a', 'b', 'c'), 'c', { gruppe: 'g1', ort: 'links' });   // [c | a b]
  assert.deepEqual(A.ansichten(a), ['c', 'a', 'b']);
  assert.deepEqual(A.ansichten(A.kacheln(a)), ['c', 'a', 'b']);
});

test('Zusammenlegen: alle Ansichten als Reiter einer Gruppe, die aktive bleibt', () => {
  let a = A.kacheln(aus('a', 'b', 'c', 'd'));
  a = A.aktivieren(a, 'b');
  const z = sauber(A.zusammenlegen(a));
  assert.equal(z.wurzel.art, 'gruppe');
  assert.deepEqual(z.wurzel.ansichten, ['a', 'b', 'c', 'd']);
  assert.equal(A.aktiveAnsicht(z), 'b');
  assert.equal(A.modus(z), 'reiter');
  assert.equal(sauber(A.zusammenlegen(A.leer())).wurzel, null);
});

// ---------------------------------------------------------------------------------------------- Ausgelagert

test('auslagern nimmt die Ansicht aus dem Baum und merkt sie; hinzufügen holt sie zurück', () => {
  let a = A.auslagern(aus('a', 'b', 'c'), 'b');
  sauber(a);
  assert.deepEqual(A.ansichten(a), ['a', 'c']);
  assert.deepEqual(a.ausgelagert, ['b']);
  a = sauber(A.hinzufuegen(a, 'b'));
  assert.deepEqual(a.ausgelagert, []);
  assert.deepEqual(A.ansichten(a), ['a', 'c', 'b']);
  // zweimal auslagern gibt keinen Doppeleintrag
  assert.deepEqual(A.auslagern(A.auslagern(aus('a', 'b'), 'b'), 'b').ausgelagert, ['b']);
});

test('entfernen streicht auch aus der Liste der ausgelagerten', () => {
  const a = A.entfernen(A.auslagern(aus('a', 'b'), 'b'), 'b');
  assert.deepEqual(a.ausgelagert, []);
});

// ---------------------------------------------------------------------------------------------- Merken und Laden

test('Anordnung übersteht Text und zurück unverändert', () => {
  let a = A.verschieben(aus('a', 'b', 'c', 'd'), 'b', { gruppe: 'g1', ort: 'rechts' });
  a = A.verschieben(a, 'c', { rand: 'unten' });
  a = A.auslagern(a, 'd');
  assert.deepEqual(A.ausText(A.alsText(a)), a);
  assert.deepEqual(A.normalisieren(JSON.parse(JSON.stringify(a))), a);
});

test('normalisieren: Müll, Falschtypen und leere Anordnung ergeben die leere Anordnung', () => {
  for (const roh of [null, undefined, 5, 'x', [], {}, { wurzel: 3 }, { wurzel: { art: 'blabla' } }, { wurzel: { art: 'gruppe', ansichten: [] } },
    { wurzel: { art: 'teilung', richtung: 'quer', kinder: [] } }, { wurzel: { art: 'teilung', richtung: 'nebeneinander', kinder: 'a' } }]) {
    assert.deepEqual(A.normalisieren(roh), A.leer(), JSON.stringify(roh));
  }
  assert.deepEqual(A.ausText('{kaputt'), A.leer());
  assert.deepEqual(A.ausText(''), A.leer());
  assert.deepEqual(A.ausText('null'), A.leer());
});

test('normalisieren: unbekannte Ansichten, Doppelte und Nicht-Texte fallen weg; leere Gruppen und Einzelteilungen lösen sich auf', () => {
  const roh = {
    version: 1, aktiveGruppe: 'g9',
    wurzel: { art: 'teilung', richtung: 'nebeneinander', anteile: [1, 1, 1], kinder: [
      { art: 'gruppe', id: 'g1', ansichten: ['a', 'x', 'a', 7, ''], aktiv: 'x' },
      { art: 'gruppe', id: 'g2', ansichten: ['x', 'y'], aktiv: 'x' },
      { art: 'gruppe', id: 'g3', ansichten: ['b', 'a'], aktiv: 'b' },
    ] },
  };
  const a = sauber(A.normalisieren(roh, new Set(['a', 'b', 'c'])));
  assert.deepEqual(A.gruppen(a).map((g) => g.ansichten), [['a'], ['b']]);
  assert.equal(a.wurzel.art, 'teilung');
  nahe(a.wurzel.anteile[0], 0.5);
  assert.equal(A.gruppen(a)[0].aktiv, 'a', 'ungültiges aktiv → letzte Ansicht der Gruppe');
  assert.ok(A.gruppen(a).some((g) => g.id === a.aktiveGruppe), 'ungültige aktiveGruppe → eine vorhandene');
});

test('normalisieren: nur eine übrige Gruppe in einer Teilung → die Gruppe selbst', () => {
  const roh = { wurzel: { art: 'teilung', richtung: 'untereinander', kinder: [{ art: 'gruppe', ansichten: ['gibts-nicht'] }, { art: 'gruppe', ansichten: ['a'] }] } };
  const a = sauber(A.normalisieren(roh, new Set(['a'])));
  assert.equal(a.wurzel.art, 'gruppe');
  assert.deepEqual(a.wurzel.ansichten, ['a']);
});

test('normalisieren: falsche Anteile werden gleichverteilt, zu kleine angehoben, die Summe ist 1', () => {
  const kind = (n) => ({ art: 'gruppe', ansichten: [n] });
  const mit = (anteile) => A.normalisieren({ wurzel: { art: 'teilung', richtung: 'nebeneinander', anteile, kinder: [kind('a'), kind('b'), kind('c')] } });
  for (const anteile of [undefined, null, 'x', [], [0, 0, 0], [-1, 2, NaN], ['a', 'b', 'c'], [1, 1]]) {
    const a = sauber(mit(anteile));
    assert.equal(a.wurzel.kinder.length, 3);
    assert.ok(a.wurzel.anteile.every((x) => x > 0));
  }
  nahe(mit([0, 0, 0]).wurzel.anteile[0], 1 / 3);
  const klein = sauber(mit([1000, 0.0001, 1000]));
  assert.ok(klein.wurzel.anteile[1] >= A.MIN_ANTEIL - 1e-12);
  const gross = sauber(mit([2, 1, 1]));
  nahe(gross.wurzel.anteile[0], 0.5); nahe(gross.wurzel.anteile[1], 0.25);
});

test('normalisieren: zu tiefe Verschachtelung wird gekappt, ohne zu hängen', () => {
  let k = { art: 'gruppe', ansichten: ['tief'] };
  for (let i = 0; i < 50; i++) k = { art: 'teilung', richtung: i % 2 ? 'nebeneinander' : 'untereinander', kinder: [k, { art: 'gruppe', ansichten: ['n' + i] }] };
  const a = sauber(A.normalisieren({ wurzel: k }));
  assert.ok(A.ansichten(a).length >= 1);
  assert.ok(A.ansichten(a).length <= A.TIEFE_MAX + 2);
});

test('normalisieren: gleichrichtige Teilungen werden zusammengezogen, Gruppen-Ids bleiben eindeutig', () => {
  const g = (id, n) => ({ art: 'gruppe', id, ansichten: [n] });
  const a = sauber(A.normalisieren({ wurzel: { art: 'teilung', richtung: 'nebeneinander', anteile: [0.5, 0.5], kinder: [
    { art: 'teilung', richtung: 'nebeneinander', anteile: [0.5, 0.5], kinder: [g('g1', 'a'), g('g1', 'b')] }, g('g2', 'c')] } }));
  assert.equal(a.wurzel.kinder.length, 3);
  nahe(a.wurzel.anteile[0], 0.25); nahe(a.wurzel.anteile[2], 0.5);
});

test('normalisieren: ausgelagert nur Texte, nur bekannte, nie zugleich im Baum', () => {
  const a = A.normalisieren({ wurzel: { art: 'gruppe', ansichten: ['a'] }, ausgelagert: ['a', 'b', 'b', 3, 'z'] }, new Set(['a', 'b']));
  assert.deepEqual(a.ausgelagert, ['b']);
  assert.deepEqual(A.normalisieren({ ausgelagert: 'b' }).ausgelagert, []);
});

test('umbenennen: Id → Kennzeichen, Unbekanntes fällt weg, Doppelte bleiben einfach', () => {
  const karte = { 1: 'GR-EG', 2: 'AN-3D', 3: 'GR-EG' };
  let a = A.verschieben(aus('1', '2', '3', '4'), '2', { gruppe: 'g1', ort: 'rechts' });
  a = A.auslagern(a, '4');
  a.ausgelagert.push('1');
  const b = sauber(A.umbenennen(A.auslagern(aus('1', '2', '3'), '2'), (x) => karte[x] ?? null));
  assert.deepEqual(A.ansichten(b), ['GR-EG']);
  assert.deepEqual(b.ausgelagert, ['AN-3D']);
  const c = sauber(A.umbenennen(a, (x) => karte[x] ?? null));
  assert.ok(!A.ansichten(c).includes('4'));
});

// ---------------------------------------------------------------------------------------------- Rechtecke

test('Rechtecke einer Gruppe: Leiste oben (22 px), Inhalt darunter, füllt die Fläche', () => {
  const g = A.layoutBerechnen(aus('a', 'b'), 800, 600);
  assert.equal(g.gruppen.length, 1);
  const r = g.gruppen[0];
  assert.deepEqual([r.x, r.y, r.b, r.h], [0, 0, 800, 600]);
  assert.deepEqual(r.leiste, { x: 0, y: 0, b: 800, h: A.LEISTE });
  assert.deepEqual(r.inhalt, { x: 0, y: A.LEISTE, b: 800, h: 600 - A.LEISTE });
  assert.deepEqual(g.trenner, []);
});

test('Rechtecke von zwei Gruppen nebeneinander: lückenlos mit Trenner dazwischen, Anteile beachtet', () => {
  let a = A.verschieben(aus('a', 'b'), 'b', { gruppe: 'g1', ort: 'rechts' });
  a = A.trennerSetzen(a, [], 0, 0.25, 0.1);
  const g = A.layoutBerechnen(a, 1004, 500);
  const [l, r] = g.gruppen;
  assert.equal(l.x, 0);
  assert.equal(l.b + A.TRENNER + r.b, 1004);
  assert.equal(r.x, l.b + A.TRENNER);
  assert.equal(l.b, 250);
  assert.equal(g.trenner.length, 1);
  const t = g.trenner[0];
  assert.deepEqual([t.x, t.y, t.b, t.h], [250, 0, A.TRENNER, 500]);
  assert.equal(t.richtung, 'nebeneinander');
  assert.deepEqual(t.pfad, []);
  assert.equal(t.gesamt, 1000);
});

test('Rechtecke untereinander und verschachtelt: alle Gruppen füllen die Fläche ohne Überlappung', () => {
  let a = A.verschieben(aus('a', 'b', 'c', 'd'), 'b', { gruppe: 'g1', ort: 'rechts' });
  const gb = A.gruppeVon(a, 'b').id;
  a = A.verschieben(A.hinzufuegen(a, 'c', { gruppe: gb }), 'c', { gruppe: gb, ort: 'unten' });
  a = A.verschieben(A.hinzufuegen(a, 'd', { gruppe: A.gruppeVon(a, 'a').id }), 'd', { gruppe: A.gruppeVon(a, 'a').id, ort: 'oben' });
  sauber(a);
  const B = 1003, H = 777;
  const geo = A.layoutBerechnen(a, B, H);
  assert.equal(geo.gruppen.length, 4);
  // Punktprobe: jeder Punkt liegt in genau einer Gruppe oder auf einem Trenner
  for (let x = 0; x < B; x += 7) for (let y = 0; y < H; y += 7) {
    const inG = geo.gruppen.filter((q) => x >= q.x && x < q.x + q.b && y >= q.y && y < q.y + q.h).length;
    const inT = geo.trenner.filter((q) => x >= q.x && x < q.x + q.b && y >= q.y && y < q.y + q.h).length;
    assert.equal(inG + (inT > 0 ? 0 : 0), inG);
    assert.ok(inG + inT >= 1, `Lücke bei ${x},${y}`);
    assert.ok(inG <= 1, `Überlappung bei ${x},${y}`);
  }
  assert.ok(geo.gruppen.every((q) => q.b >= 0 && q.h >= 0));
});

test('Rechtecke: leere Anordnung und unbrauchbare Grösse ergeben nichts', () => {
  assert.deepEqual(A.layoutBerechnen(A.leer(), 800, 600), { gruppen: [], trenner: [] });
  assert.deepEqual(A.layoutBerechnen(aus('a'), 0, 600), { gruppen: [], trenner: [] });
  assert.deepEqual(A.layoutBerechnen(aus('a'), 800, NaN), { gruppen: [], trenner: [] });
});

test('trennerPosition ist die Umkehrung der Rechtecke (Zeiger auf Mitte des Trenners → Anteil)', () => {
  let a = A.verschieben(aus('a', 'b'), 'b', { gruppe: 'g1', ort: 'rechts' });
  a = A.trennerSetzen(a, [], 0, 0.3, 0.1);
  for (const [B, H] of [[1004, 600], [1500, 300]]) {
    const t = A.layoutBerechnen(a, B, H).trenner[0];
    const p = A.trennerPosition(t, t.x + t.b / 2, t.y + t.h / 2);
    assert.ok(Math.abs(p - 0.3) < 1 / t.gesamt + 1e-9, `${p}`);
  }
  let u = A.verschieben(aus('a', 'b'), 'b', { gruppe: 'g1', ort: 'unten' });
  u = A.trennerSetzen(u, [], 0, 0.6, 0.1);
  const tu = A.layoutBerechnen(u, 800, 704).trenner[0];
  assert.equal(tu.richtung, 'untereinander');
  assert.ok(Math.abs(A.trennerPosition(tu, 400, tu.y + tu.h / 2) - 0.6) < 0.01);
});

// ---------------------------------------------------------------------------------------------- Andockziele

const zwei = () => {
  const a = A.verschieben(aus('a', 'b', 'c'), 'c', { gruppe: 'g1', ort: 'rechts' });   // [a b | c]
  return { a, geo: A.layoutBerechnen(a, 1004, 600) };
};
const tabsVon = (geo) => Object.fromEntries(geo.gruppen.map((g) => [g.id, [{ x: g.x, b: 100 }, { x: g.x + 101, b: 100 }].slice(0, g.id === geo.gruppen[0].id ? 2 : 1)]));

test('dropZiel: Mitte einer Gruppe = als Reiter dazu (ganze Fläche als Vorschau)', () => {
  const { geo } = zwei();
  const g = geo.gruppen[1];
  const z = A.dropZiel(geo, g.inhalt.x + g.inhalt.b / 2, g.inhalt.y + g.inhalt.h / 2, { breite: 1004, hoehe: 600, quelle: { gruppe: geo.gruppen[0].id, nurEine: false } });
  assert.equal(z.art, 'gruppe'); assert.equal(z.gruppe, g.id); assert.equal(z.ort, 'mitte');
  assert.deepEqual(z.vorschau, g.inhalt);
});

test('dropZiel: Randzonen einer Gruppe = teilen, Vorschau ist die Hälfte', () => {
  const { geo } = zwei();
  const g = geo.gruppen[0], q = { gruppe: geo.gruppen[1].id, nurEine: true };
  const p = (fx, fy) => [g.inhalt.x + g.inhalt.b * fx, g.inhalt.y + g.inhalt.h * fy];
  const aufg = (fx, fy) => A.dropZiel(geo, ...p(fx, fy), { breite: 1004, hoehe: 600, quelle: q });
  const l = aufg(0.15, 0.4); assert.equal(l.ort, 'links'); assert.equal(l.vorschau.b, Math.round(g.inhalt.b / 2));
  assert.equal(aufg(0.85, 0.4).ort, 'rechts');
  const o = aufg(0.4, 0.1); assert.equal(o.ort, 'oben'); assert.equal(o.vorschau.h, Math.round(g.inhalt.h / 2)); assert.equal(o.vorschau.y, g.inhalt.y);
  const u = aufg(0.4, 0.9); assert.equal(u.ort, 'unten'); assert.equal(u.vorschau.y + u.vorschau.h, g.inhalt.y + g.inhalt.h);
  assert.equal(aufg(0.4, 0.4).ort, 'mitte');
  assert.equal(aufg(0.08, 0.2).ort, 'links', 'in der Ecke gewinnt die nähere Kante');
  assert.equal(aufg(0.2, 0.05).ort, 'oben');
});

test('dropZiel: Andockziele (Kreuz) haben Vorrang vor den Zonen', () => {
  const { geo } = zwei();
  const g = geo.gruppen[0], lp = A.leitpunkte(g.inhalt);
  for (const ort of A.ORTE) {
    const r = lp[ort];
    const z = A.dropZiel(geo, r.x + r.b / 2, r.y + r.h / 2, { breite: 1004, hoehe: 600, quelle: { gruppe: geo.gruppen[1].id, nurEine: true } });
    assert.equal(z.ort, ort, ort);
    assert.equal(z.gruppe, g.id);
  }
  // die fünf Kästchen überlappen nicht und liegen in der Gruppe
  const rs = Object.values(lp);
  for (let i = 0; i < rs.length; i++) for (let j = i + 1; j < rs.length; j++)
    assert.ok(rs[i].x + rs[i].b <= rs[j].x || rs[j].x + rs[j].b <= rs[i].x || rs[i].y + rs[i].h <= rs[j].y || rs[j].y + rs[j].h <= rs[i].y);
});

test('dropZiel: über der Reiterleiste = Einfügestelle zwischen den Reitern mit Marke', () => {
  const { geo } = zwei();
  const g = geo.gruppen[0];
  const tabs = tabsVon(geo);   // Reiter a: 0–100, b: 101–201
  const y = g.leiste.y + 10;
  const opt = { breite: 1004, hoehe: 600, tabs, quelle: { gruppe: geo.gruppen[1].id, nurEine: true } };
  assert.equal(A.dropZiel(geo, 150, y, opt).index, 1);         // links der Mitte von b
  assert.equal(A.dropZiel(geo, 190, y, opt).index, 2);         // rechts der Mitte von b
  assert.equal(A.dropZiel(geo, 20, y, opt).index, 0);
  const z = A.dropZiel(geo, 190, y, opt);
  assert.equal(z.ort, 'mitte');
  assert.equal(z.marke.x, 201); assert.equal(z.marke.y, g.leiste.y); assert.equal(z.marke.h, g.leiste.h);
  const leerLeiste = A.dropZiel(geo, geo.gruppen[1].x + 5, y, { ...opt, quelle: { gruppe: g.id, nurEine: false } });
  assert.equal(leerLeiste.gruppe, geo.gruppen[1].id);
});

test('dropZiel: Rand der ganzen Fläche (links, rechts, unten), nicht auf Reiterleisten', () => {
  const { geo } = zwei();
  const opt = { breite: 1004, hoehe: 600, quelle: { gruppe: geo.gruppen[0].id, nurEine: false } };
  const l = A.dropZiel(geo, 5, 300, opt); assert.equal(l.art, 'rand'); assert.equal(l.ort, 'links'); assert.deepEqual(l.vorschau, { x: 0, y: 0, b: Math.round(1004 * 0.3), h: 600 });
  assert.equal(A.dropZiel(geo, 1000, 300, opt).ort, 'rechts');
  const u = A.dropZiel(geo, 400, 595, opt); assert.equal(u.art, 'rand'); assert.equal(u.ort, 'unten'); assert.equal(u.vorschau.y + u.vorschau.h, 600);
  const leiste = A.dropZiel(geo, 5, 10, { ...opt, tabs: tabsVon(geo) });
  assert.equal(leiste.art, 'gruppe', 'links oben auf der Reiterleiste gilt «Reiter einfügen»');
});

test('dropZiel: kein Ziel ausserhalb der Fläche, ohne Gruppen und für die einzige eigene Ansicht', () => {
  const { geo } = zwei();
  const opt = { breite: 1004, hoehe: 600 };
  assert.equal(A.dropZiel(geo, -1, 100, opt), null);
  assert.equal(A.dropZiel(geo, 100, 700, opt), null);
  assert.equal(A.dropZiel(geo, 1004, 100, opt), null);
  assert.equal(A.dropZiel({ gruppen: [], trenner: [] }, 10, 10, opt), null);
  const eins = A.layoutBerechnen(aus('a'), 800, 600);
  const g = eins.gruppen[0];
  const q = { gruppe: g.id, nurEine: true };
  assert.equal(A.dropZiel(eins, 5, 300, { breite: 800, hoehe: 600, quelle: q }), null, 'allein in der einzigen Gruppe: nichts zu tun');
  assert.equal(A.dropZiel(eins, 400, 300, { breite: 800, hoehe: 600, quelle: q }), null);
  assert.equal(A.dropZiel(eins, 5, 300, { breite: 800, hoehe: 600, quelle: { gruppe: g.id, nurEine: false } }).art, 'rand');
  // in einer Zweiergruppe: die eigene Einzel-Ansicht nicht an die eigene Gruppe, wohl aber an die andere
  const { geo: g2 } = zwei();
  const eigene = g2.gruppen[1];
  assert.equal(A.dropZiel(g2, eigene.inhalt.x + 5, eigene.inhalt.y + 100, { breite: 1004, hoehe: 600, quelle: { gruppe: eigene.id, nurEine: true } }), null);
});

test('dropZiel über dem Trenner nimmt die nächste Gruppe', () => {
  const { geo } = zwei();
  const t = geo.trenner[0];
  const z = A.dropZiel(geo, t.x + 1, 300, { breite: 1004, hoehe: 600, quelle: { gruppe: geo.gruppen[0].id, nurEine: false } });
  assert.ok(z && z.art === 'gruppe');
});

test('zielAnwenden: setzt ein Ziel aus dropZiel um (Rand, Teilen, Reiter dazu, kein Ziel)', () => {
  const a = aus('a', 'b', 'c');
  assert.equal(A.zielAnwenden(a, 'c', null), a);
  const r = sauber(A.zielAnwenden(a, 'c', { art: 'rand', ort: 'rechts' }));
  assert.deepEqual(A.gruppen(r).map((g) => g.ansichten), [['a', 'b'], ['c']]);
  const t = sauber(A.zielAnwenden(a, 'c', { art: 'gruppe', gruppe: 'g1', ort: 'unten' }));
  assert.equal(t.wurzel.richtung, 'untereinander');
  const m = sauber(A.zielAnwenden(a, 'c', { art: 'gruppe', gruppe: 'g1', ort: 'mitte', index: 0 }));
  assert.deepEqual(A.ansichten(m), ['c', 'a', 'b']);
});

// ---------------------------------------------------------------------------------------------- Zweitfenster (reine Teile)

test('Adresse des Zweitfensters: bauen und lesen', () => {
  const u = Z.fensterUrl('http://localhost:5178/?start=1#x', 'AN-3D', 2);
  assert.equal(u, 'http://localhost:5178/?ansicht=AN-3D&fenster=2');
  assert.deepEqual(Z.parameterLesen(new URL(u).search), { nummer: 2, ansichten: ['AN-3D'] });
  const mehr = Z.fensterUrl('http://localhost:5178/', ['GR-EG', 'GR-1.OG'], 3);
  assert.deepEqual(Z.parameterLesen(new URL(mehr).search), { nummer: 3, ansichten: ['GR-EG', 'GR-1.OG'] });
  const sonder = Z.fensterUrl('http://localhost:5178/', 'Ansicht & Co, "1"', 2);
  assert.deepEqual(Z.parameterLesen(new URL(sonder).search).ansichten, ['Ansicht & Co', '"1"'], 'Sonderzeichen überstehen die Adresse (Komma trennt)');
});

test('Adresse lesen: ungültige Werte ergeben das Hauptfenster', () => {
  for (const s of ['', '?', '?fenster=1', '?fenster=0', '?fenster=abc', '?fenster=2.5', '?fenster=10', '?fenster=-2', null, undefined, '?ansicht=GR-EG']) {
    assert.deepEqual(Z.parameterLesen(s), { nummer: 1, ansichten: [] }, String(s));
  }
  assert.deepEqual(Z.parameterLesen('?fenster=2'), { nummer: 2, ansichten: [] });
  assert.deepEqual(Z.parameterLesen('?fenster=4&ansicht=,a,,b ,'), { nummer: 4, ansichten: ['a', 'b'] });
});

test('freieNummer: kleinste freie ab 2, null wenn alle belegt', () => {
  assert.equal(Z.freieNummer([]), 2);
  assert.equal(Z.freieNummer([2]), 3);
  assert.equal(Z.freieNummer([3, 2, 5]), 4);
  assert.equal(Z.freieNummer([2, 3, 4, 5, 6, 7, 8, 9]), null);
});

test('fensterOptionen: Grösse, Lage an der Stelle des Loslassens, nie negativ', () => {
  assert.equal(Z.fensterOptionen(), 'popup=yes,width=1100,height=750');
  assert.equal(Z.fensterOptionen({ x: 2000, y: 300, breite: 800, hoehe: 600 }), 'popup=yes,width=800,height=600,left=1940,top=280');
  assert.match(Z.fensterOptionen({ x: 10, y: 5 }), /left=0,top=0$/);
  assert.equal(Z.fensterOptionen({ x: NaN, y: 3 }), 'popup=yes,width=1100,height=750');
});

test('ausserhalb des Browsers sind Fensternummer und Zweitfenster-Frage ruhig (Hauptfenster)', () => {
  assert.equal(Z.fensterNummer(), 1);
  assert.equal(Z.istZweitfenster(), false);
  assert.deepEqual(Z.startAnsichten(), []);
});

// ---------------------------------------------------------------------------------------------- Zusammenspiel der Dateien

test('Einbindung: anordnung.css ist in index.html, die Reiterleiste ist nicht mehr fest im HTML, die Fläche ist Wurzel der Kacheln', () => {
  const html = readFileSync(path.join(hier, '../../index.html'), 'utf8');
  assert.match(html, /css\/anordnung\.css/);
  assert.doesNotMatch(html, /<div id="ansichtsreiter"/);
  assert.match(html, /<div id="ansichtsflaeche">/);
  const css = readFileSync(path.join(hier, '../../css/anordnung.css'), 'utf8');
  for (const k of ['.reiterleiste', '.ag-trenner', '.ag-vorschau', '.ag-lp', '.ag-rand', '.ag-schatten', 'body[data-fenster]']) assert.ok(css.includes(k), k);
  assert.doesNotMatch(css.match(/\.reiterleiste \{[^}]*\}/)[0], /overflow(-x)?\s*:\s*(auto|scroll)/, 'Reiterleiste rollt nie');
  const hl = readFileSync(path.join(hier, '../../css/hlks.css'), 'utf8');
  assert.match(hl, /\.kachel \{ position: absolute;/);
});
