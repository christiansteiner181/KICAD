// Tests der reinen Logik der Familien-Oberfläche (Modul 2, Auftrag C1) ohne Browser: Tabellen ↔ ops, Formel-Felder, Typkatalog-Zellen, Körper und
// Konturen, Anschlüsse, Fehler des Kerns, Typauswahl und Vorschau beim Platzieren, Projektbrowser-Knoten, Palette, Verdrahtung (Menüband, Symbole,
// Aktionen, Fenster). Aufruf:  node --test src/Hlks.Web/wwwroot/js/familien/familien.test.mjs
// Die Testfamilie ist die fiktive Familie des Hauptlaufs (tests/daten/familien/test_heizkoerper.familie.json): alle Werte fiktiv.
// Mit HLKS_TEST_SERVER (Server mit offenem Projekt) prüft ein zusätzlicher Test, dass der Kern alle hier gebauten Platzhalter und ops annimmt
// (nur Trockenläufe und Abfragen, nichts wird geändert).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const T = await import('./familien-text.js');
const A = await import('./familien-ansicht.js');
const P = await import('./familien-palette.js');
const Q = await import('./typauswahl.js');
const B = await import('./browser-knoten.js');
const E = await import('./editor-bausteine.js');
const sym = await import('../oberflaeche/symbole.js');

const lies = (rel) => readFileSync(path.resolve(hier, rel), 'utf8');
const testFamilie = JSON.parse(lies('../../../../../tests/daten/familien/test_heizkoerper.familie.json'));
const klon = (x) => JSON.parse(JSON.stringify(x));
const nah = (a, b, eps = 1e-6) => assert.ok(Math.abs(a - b) <= eps, `${a} ≠ ${b}`);

// ================================================================================================ Zahlen und Formeln

test('Eingabe: Zahl, Formel oder leer (Komma als Dezimalzeichen, führendes «=» erzwingt Formel)', () => {
  assert.equal(T.zahlLesen('12'), 12);
  assert.equal(T.zahlLesen(' 1,5 '), 1.5);
  assert.equal(T.zahlLesen('-0.25'), -0.25);
  assert.equal(T.zahlLesen('1e3'), 1000);
  assert.equal(T.zahlLesen('abc'), null);
  assert.equal(T.zahlLesen('1 + 2'), null);
  assert.equal(T.zahlLesen(''), null);
  assert.equal(T.zahlOderFormel('600'), 600);
  assert.equal(T.zahlOderFormel('laenge - 50'), 'laenge - 50');
  assert.equal(T.zahlOderFormel('= 5'), '5', 'mit «=» bleibt es eine Formel');
  assert.equal(T.zahlOderFormel('  '), undefined);
  assert.equal(T.feldText(600), '600');
  assert.equal(T.feldText(0.1 + 0.2), '0.3');
  assert.equal(T.feldText('laenge / 2'), 'laenge / 2');
  assert.equal(T.feldText(undefined), '');
});

test('Wert oder Formel je Datentyp: Zahl → Wert, sonst Formel; Text braucht «=»; ja/nein erkennt ja, nein, true, false', () => {
  assert.deepEqual(T.wertEingabe('laenge', '100'), { wert: 100 });
  assert.deepEqual(T.wertEingabe('laenge', 'bodenabstand + hoehe'), { formel: 'bodenabstand + hoehe' });
  assert.deepEqual(T.wertEingabe('laenge', '= bodenabstand + hoehe'), { formel: 'bodenabstand + hoehe' });
  assert.deepEqual(T.wertEingabe('laenge', ''), { leer: true });
  assert.deepEqual(T.wertEingabe('text', 'Stahl'), { wert: 'Stahl' });
  assert.deepEqual(T.wertEingabe('text', '= if(a > 1, "x", "y")'), { formel: 'if(a > 1, "x", "y")' });
  assert.deepEqual(T.wertEingabe('medium', 'HZ-VL'), { wert: 'HZ-VL' });
  assert.deepEqual(T.wertEingabe('ja_nein', 'ja'), { wert: true });
  assert.deepEqual(T.wertEingabe('ja_nein', 'Nein'), { wert: false });
  assert.deepEqual(T.wertEingabe('ja_nein', 'hoehe > 500'), { formel: 'hoehe > 500' });
  assert.equal(T.wertAnzeige({ formel: 'bodenabstand + hoehe' }), '= bodenabstand + hoehe');
  assert.equal(T.wertAnzeige({ wert: 150 }), '150');
  assert.equal(T.wertAnzeige({ wert: true }), 'ja');
  assert.equal(T.wertAnzeige({}), '');
});

test('Namen in Formeln: ohne Funktionsnamen und ohne Texte; Ersetzen nur ganzer Namen', () => {
  assert.deepEqual(T.bezeichner('bodenabstand + hoehe'), ['bodenabstand', 'hoehe']);
  assert.deepEqual(T.bezeichner('if(mit_abdeckung, laenge + 5, 0)'), ['mit_abdeckung', 'laenge']);
  assert.deepEqual(T.bezeichner('if(typ = "hoehe", a, b)'), ['typ', 'a', 'b'], 'Text in Anführungszeichen zählt nicht');
  assert.deepEqual(T.bezeichner('pi() * r ^ 2 + sqrt (x)'), ['r', 'x'], 'sqrt vor der Klammer ist eine Funktion, x ein Name');
  assert.equal(T.nameErsetzen('hoehe + hoehe2 + "hoehe" + max(hoehe, 1)', 'hoehe', 'h'), 'h + hoehe2 + "hoehe" + max(h, 1)');
  assert.equal(T.nameErsetzen('laenge', 'laenge', 'baulaenge'), 'baulaenge');
});

// ================================================================================================ Parameter

test('Parameterzeile: ein Feld ändern ergibt ein sauberes Parameterobjekt (Formel geht vor Wert, Einheit nur bei Zahl)', () => {
  const p = testFamilie.parameter.find((x) => x.name === 'oberkante');
  assert.equal(T.wertAnzeige(p), '= bodenabstand + hoehe');
  const nach = T.parameterMitFeld(p, 'eingabe', '500');
  assert.equal(nach.wert, 500); assert.equal(nach.formel, undefined);
  const wieder = T.parameterMitFeld(nach, 'eingabe', 'bodenabstand + 2 * hoehe');
  assert.equal(wieder.formel, 'bodenabstand + 2 * hoehe'); assert.equal(wieder.wert, undefined);
  const leer = T.parameterMitFeld(p, 'eingabe', '');
  assert.equal(leer.formel, undefined); assert.equal(leer.wert, undefined);
  assert.equal(T.parameterMitFeld(p, 'beschreibung', 'neu').beschreibung, 'neu');
  assert.equal(T.parameterMitFeld(p, 'art', 'typ').art, 'typ');
  const leistung = T.parameterMitFeld(testFamilie.parameter.find((x) => x.name === 'leistung'), 'einheit', 'kW');
  assert.equal(leistung.einheit, 'kW');
  const alsLaenge = T.parameterMitFeld(testFamilie.parameter.find((x) => x.name === 'leistung'), 'datentyp', 'laenge');
  assert.equal(alsLaenge.einheit, undefined, 'laenge hat immer mm, keine Einheit im Schema');
  // Datentyp wechseln liest den Vorgabewert neu ein
  const ja = T.parameterMitFeld(testFamilie.parameter.find((x) => x.name === 'tiefe'), 'datentyp', 'ja_nein');
  assert.equal(ja.formel, '100', 'eine Zahl ist für ja/nein keine Wahrheit, sie wird zur Formel (der Kern meldet das)');
  // ändert nichts am Original
  assert.equal(p.formel, 'bodenabstand + hoehe');
});

test('Neuer Parameter: Dialogfelder → Objekt; Prüfung des Namens', () => {
  const def = testFamilie;
  assert.deepEqual(T.parameterNeu({ name: ' hoehe2 ', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung', eingabe: '700' }), { name: 'hoehe2', art: 'typ', datentyp: 'laenge', gruppe: 'abmessung', wert: 700 });
  assert.deepEqual(T.parameterNeu({ name: 'p', art: 'exemplar', datentyp: 'laenge', eingabe: 'hoehe / 2', ziel: 'x', beschreibung: 'd' }),
    { name: 'p', art: 'exemplar', datentyp: 'laenge', gruppe: 'sonstiges', formel: 'hoehe / 2', ziel: 'x', beschreibung: 'd' });
  assert.equal(T.parameterNeu({ name: 'k', datentyp: 'zahl', einheit: 'kW' }).einheit, 'kW');
  assert.equal(T.parameterNeu({ name: 'k', datentyp: 'laenge', einheit: 'mm' }).einheit, undefined);
  assert.equal(T.parameterPruefen(def, { name: '' }), 'Name fehlt.');
  assert.match(T.parameterPruefen(def, { name: '1abc' }), /kein gültiger Parametername/);
  assert.match(T.parameterPruefen(def, { name: 'a b' }), /kein gültiger Parametername/);
  assert.match(T.parameterPruefen(def, { name: 'hoehe' }), /gibt es schon/);
  assert.equal(T.parameterPruefen(def, { name: 'höhe_2' }), true, 'Umlaute und _ sind erlaubt');
});

test('Verwendungen: Formeln, Körper, Anschlüsse, Typwerte – die eigene Formel zählt nicht', () => {
  const v = T.verwendungenVon(testFamilie, 'hoehe');
  assert.ok(v.some((x) => x.tab === 'parameter' && x.name === 'oberkante' && x.feld === 'formel'), 'oberkante = bodenabstand + hoehe');
  assert.ok(v.some((x) => x.tab === 'typen' && x.name === '22-600-1000'), 'Typwerte');
  const laenge = T.verwendungenVon(testFamilie, 'laenge');
  assert.ok(laenge.some((x) => x.tab === 'koerper' && x.name === 'platten' && x.feld === 'bis'));
  assert.ok(laenge.some((x) => x.tab === 'koerper' && x.name === 'abdeckung'));
  assert.ok(laenge.some((x) => x.tab === 'anschluesse' && x.name === 'vl' && x.feld === 'lage'));
  assert.ok(T.verwendungenVon(testFamilie, 'mit_abdeckung').some((x) => x.tab === 'koerper' && x.feld === 'sichtbar'));
  assert.ok(T.verwendungenVon(testFamilie, 'dn').some((x) => x.tab === 'anschluesse' && x.feld === 'dn'));
  assert.deepEqual(T.verwendungenVon(testFamilie, 'beschreibung_gibts_nicht'), []);
  assert.deepEqual(T.blockierendeVerwendungen(T.verwendungenVon(testFamilie, 'leistung')), [], 'leistung steht nur in Typwerten');
  assert.ok(T.blockierendeVerwendungen(v).length > 0);
  // Medium-Parameter in einem Anschluss
  const d = klon(testFamilie);
  d.parameter.push({ name: 'medium1', art: 'typ', datentyp: 'medium', wert: 'HZ-VL' });
  d.anschluesse[0].medium = 'medium1';
  assert.ok(T.verwendungenVon(d, 'medium1').some((x) => x.feld === 'medium'));
});

test('Umbenennen führt Formeln, Körper, Anschlüsse und Typwerte nach; der alte Name wird zuletzt gelöscht', () => {
  const u = T.umbenennenOps(testFamilie, 'hoehe', 'bauhoehe');
  assert.equal(u.fehler, undefined);
  const ops = u.ops;
  assert.equal(ops[0].op, 'parameter_setzen'); assert.equal(ops[0].parameter.name, 'bauhoehe');
  assert.deepEqual(ops.at(-1), { op: 'parameter_loeschen', name: 'hoehe' });
  const ober = ops.find((o) => o.op === 'parameter_setzen' && o.parameter.name === 'oberkante');
  assert.equal(ober.parameter.formel, 'bodenabstand + bauhoehe');
  const typ = ops.find((o) => o.op === 'typ_setzen' && o.typ.name === '22-600-1000');
  assert.equal(typ.typ.werte.bauhoehe, 600); assert.equal(typ.typ.werte.hoehe, undefined);
  assert.ok(ops.find((o) => o.op === 'typ_setzen' && o.typ.name === '22-900-800').typ.werte.bauhoehe === 900);
  assert.deepEqual(u.verwendungen.map((x) => x.tab).filter((t, i, a) => a.indexOf(t) === i).sort(), ['parameter', 'typen']);
  // ein Name in Körpern: laenge
  const l = T.umbenennenOps(testFamilie, 'laenge', 'baulaenge');
  const platten = l.ops.find((o) => o.op === 'koerper_setzen' && o.koerper.id === 'platten');
  assert.deepEqual(platten.koerper.bis, ['baulaenge', 'tiefe', 'oberkante']);
  const abd = l.ops.find((o) => o.op === 'koerper_setzen' && o.koerper.id === 'abdeckung');
  assert.deepEqual(abd.koerper.bis, ['baulaenge + 5', 'tiefe + 5', 'oberkante + 10']);
  assert.equal(abd.koerper.sichtbar, 'mit_abdeckung', 'andere Felder bleiben');
  const vl = l.ops.find((o) => o.op === 'anschluss_setzen' && o.anschluss.name === 'vl');
  assert.deepEqual(vl.anschluss.lage, ['baulaenge - randabstand', 'tiefe / 2', 'bodenabstand - stutzen']);
  // nicht betroffene Einträge werden nicht gesendet
  assert.equal(l.ops.some((o) => o.op === 'koerper_setzen' && o.koerper.id === 'xyz'), false);
  // Fehler
  assert.match(T.umbenennenOps(testFamilie, 'hoehe', 'laenge').fehler, /gibt es schon/);
  assert.match(T.umbenennenOps(testFamilie, 'hoehe', '2x').fehler, /kein gültiger/);
  assert.match(T.umbenennenOps(testFamilie, 'gibtsnicht', 'x').fehler, /gibt es nicht/);
  // Original unverändert
  assert.equal(testFamilie.parameter.find((p) => p.name === 'oberkante').formel, 'bodenabstand + hoehe');
});

// ================================================================================================ Typen

test('Typkatalog: Spalten sind die Typparameter ohne Formel', () => {
  const sp = T.typSpalten(testFamilie).map((p) => p.name);
  assert.deepEqual(sp, ['hoehe', 'laenge', 'tiefe', 'anschlussabstand', 'randabstand', 'stutzen', 'dn', 'mit_abdeckung', 'leistung']);
  assert.ok(!sp.includes('oberkante') && !sp.includes('bodenabstand'));
});

test('Typkatalog: Zellen lesen (Zahl, ja/nein, leer), Typ mit Zelle, Kopie, Umbenennen, freie Namen', () => {
  const hoehe = testFamilie.parameter.find((p) => p.name === 'hoehe');
  const abd = testFamilie.parameter.find((p) => p.name === 'mit_abdeckung');
  const typ = testFamilie.typen[0];
  assert.deepEqual(T.typZelleLesen(hoehe, '650'), { wert: 650 });
  assert.deepEqual(T.typZelleLesen(hoehe, '650,5'), { wert: 650.5 });
  assert.deepEqual(T.typZelleLesen(hoehe, ''), { leer: true });
  assert.match(T.typZelleLesen(hoehe, 'laenge / 2').fehler, /Zahl erwartet/);
  assert.deepEqual(T.typZelleLesen(abd, 'nein'), { wert: false });
  assert.deepEqual(T.typZelleLesen(abd, 'ja'), { wert: true });
  assert.match(T.typZelleLesen(abd, 'vielleicht').fehler, /ja oder nein/);
  assert.equal(T.typZelleText(abd, false), 'nein');
  assert.equal(T.typZelleText(hoehe, 600), '600');
  assert.equal(T.typZelleText(hoehe, undefined), '');
  const n = T.typMitWert(typ, hoehe, '650');
  assert.equal(n.typ.werte.hoehe, 650); assert.equal(typ.werte.hoehe, 600, 'Original unverändert');
  assert.equal(T.typMitWert(typ, hoehe, '').typ.werte.hoehe, undefined, 'leer entfernt den Wert');
  assert.ok(T.typMitWert(typ, hoehe, 'x').fehler);
  assert.deepEqual(T.typKopie({ name: 'A', werte: { hoehe: 1 }, quellen: { hoehe: ['x'] } }, 'B'), { name: 'B', werte: { hoehe: 1 } });
  assert.deepEqual(T.typUmbenennenOps(typ, '22-600-1100'), [{ op: 'typ_setzen', typ: { ...typ, name: '22-600-1100' } }, { op: 'typ_loeschen', name: '22-600-1000' }]);
  assert.equal(T.freierName('Typ', ['Typ']), 'Typ 2');
  assert.equal(T.freierName('Typ', ['typ', 'Typ 2']), 'Typ 3');
  assert.equal(T.freierName('Typ', []), 'Typ');
});

// ================================================================================================ Körper

test('Körper: Felder je Art, Platzhalter, freie Ids, Detailgrad', () => {
  assert.deepEqual(T.KOERPER_ARTEN.map(([a]) => a), ['quader', 'zylinder', 'kegel', 'kugel', 'extrusion', 'rotation', 'sweep']);
  assert.deepEqual(T.koerperFelder('quader').map((f) => f.pfad), ['von', 'bis']);
  assert.deepEqual(T.koerperFelder('zylinder').map((f) => f.pfad), ['von', 'nach', 'r']);
  assert.deepEqual(T.koerperFelder('kegel').map((f) => f.pfad), ['von', 'nach', 'r1', 'r2']);
  assert.deepEqual(T.koerperFelder('kugel').map((f) => f.pfad), ['mitte', 'r']);
  assert.deepEqual(T.koerperFelder('extrusion').map((f) => f.pfad), ['ebene', 'von', 'bis', 'kontur', 'loecher']);
  assert.deepEqual(T.koerperFelder('rotation').map((f) => f.pfad), ['ebene', 'kontur', 'achse.punkt', 'achse.richtung', 'winkel']);
  assert.deepEqual(T.koerperFelder('sweep').map((f) => f.pfad), ['profil', 'pfad', 'biegeradius']);
  assert.deepEqual(T.koerperFelder('gibtsnicht'), []);
  assert.ok(T.koerperFelder('rotation').find((f) => f.pfad === 'winkel').optional);
  for (const [art] of T.KOERPER_ARTEN) {
    const k = T.koerperNeu(art, `${art}1`, 'Name');
    assert.equal(k.art, art); assert.equal(k.id, `${art}1`);
    for (const f of T.koerperFelder(art).filter((x) => !x.optional && x.typ !== 'loecher')) assert.notEqual(T.pfadLesen(k, f.pfad), undefined, `${art}.${f.pfad} hat einen Platzhalter`);
  }
  assert.equal(T.freieKoerperId(testFamilie, 'quader'), 'quader1');
  assert.equal(T.freieKoerperId({ koerper: [{ id: 'quader1' }, { id: 'quader2' }] }, 'quader'), 'quader3');
  assert.deepEqual(T.koerperDetailgrade({}), ['grob', 'mittel', 'fein']);
  assert.deepEqual(T.koerperDetailgrade(testFamilie.koerper[1]), ['mittel', 'fein']);
  assert.equal(T.detailgradKurz(testFamilie.koerper[1]), '– m f');
  assert.equal(T.detailgradKurz(testFamilie.koerper[0]), 'g m f');
  const nur = T.detailgradSetzen(testFamilie.koerper[0], 'grob', false);
  assert.deepEqual(nur.koerper.detailgrad, ['mittel', 'fein']);
  assert.equal(T.detailgradSetzen(nur.koerper, 'grob', true).koerper.detailgrad, undefined, 'alle drei = Vorgabe, Feld entfällt');
  assert.match(T.detailgradSetzen({ detailgrad: ['fein'] }, 'fein', false).fehler, /Mindestens ein Detailgrad/);
});

test('Pfade: lesen, setzen (Kopie), entfernen räumt leere Zwischenobjekte weg', () => {
  const k = T.koerperNeu('rotation', 'r1');
  assert.deepEqual(T.pfadLesen(k, 'achse.punkt'), [0, 0]);
  const n = T.pfadSetzen(k, 'achse.punkt', [5, 5]);
  assert.deepEqual(n.achse.punkt, [5, 5]); assert.deepEqual(k.achse.punkt, [0, 0]);
  assert.equal(T.pfadSetzen(k, 'achse.richtung', undefined).achse.punkt !== undefined, true);
  const ohne = T.pfadSetzen(T.pfadSetzen(k, 'achse.richtung', undefined), 'achse.punkt', undefined);
  assert.equal(ohne.achse, undefined);
  assert.equal(T.pfadSetzen({}, 'stil.stift', 'dick').stil.stift, 'dick');
  assert.equal(T.pfadSetzen({ stil: { stift: 'dick' } }, 'stil.stift', undefined).stil, undefined);
  assert.equal(T.pfadLesen({}, 'a.b.c'), undefined);
});

test('Punkte und Konturen: Zeilen aus Texten, Wölbung optional, Formeln erlaubt', () => {
  assert.deepEqual(T.punktAusTexten(['0', '100', 'hoehe / 2'], 3), { punkt: [0, 100, 'hoehe / 2'] });
  assert.ok(T.punktAusTexten(['0', '', '1'], 3).fehler);
  assert.deepEqual(T.konturZeile(['10', '20', '']), { punkt: [10, 20] });
  assert.deepEqual(T.konturZeile(['10', '20', '0.5']), { punkt: [10, 20, 0.5] });
  assert.deepEqual(T.konturZeile(['laenge', '0', 'tan(22.5)']), { punkt: ['laenge', 0, 'tan(22.5)'] });
  assert.ok(T.konturZeile(['', '5', '']).fehler);
  assert.deepEqual(T.profilWechseln('rechteck'), { rechteck: [20, 20] });
  assert.deepEqual(T.profilWechseln('kreis'), { kreis: 10 });
  assert.equal(T.profilArt({ rechteck: [1, 2] }), 'rechteck');
  assert.equal(T.profilArt({ kreis: 4 }), 'kreis');
});

test('Sinnbild: Felder und Platzhalter je Art', () => {
  assert.deepEqual(T.SINNBILD_ARTEN.map(([a]) => a), ['linie', 'polylinie', 'polygon', 'kreis', 'bogen', 'text']);
  for (const [art] of T.SINNBILD_ARTEN) {
    const s = T.sinnbildNeu(art);
    assert.equal(s.art, art);
    for (const f of T.sinnbildFelder(art).filter((x) => !x.optional)) assert.notEqual(T.pfadLesen(s, f.pfad), undefined, `${art}.${f.pfad}`);
  }
  assert.equal(T.sinnbildFelder('kreis').find((f) => f.pfad === 'punkte').max, 1);
  assert.equal(T.sinnbildFelder('linie').find((f) => f.pfad === 'punkte').max, 2);
  assert.ok(T.sinnbildFelder('polygon').find((f) => f.pfad === 'punkte').min === 3);
});

// ================================================================================================ Anschlüsse

test('Anschluss: Grösse passend zur Art (rohr dn · kanal rund d · kanal rechteckig b, h · elektro keine)', () => {
  assert.deepEqual(T.anschlussGroesse({ art: 'rohr' }), ['dn']);
  assert.deepEqual(T.anschlussGroesse({}), ['dn']);
  assert.deepEqual(T.anschlussGroesse({ art: 'kanal', form: 'rund' }), ['d']);
  assert.deepEqual(T.anschlussGroesse({ art: 'kanal', form: 'rechteck' }), ['b', 'h']);
  assert.deepEqual(T.anschlussGroesse({ art: 'elektro' }), []);
});

test('Anschluss: neu ohne erfundene Grösse; Felder ändern; Art wechseln räumt unpassende Grössen auf', () => {
  const a = T.anschlussNeu({ name: ' vl ', medium: 'HZ-VL', fluss: 'rein' });
  assert.deepEqual(a, { name: 'vl', medium: 'HZ-VL', fluss: 'rein', art: 'rohr', form: 'rund', lage: [0, 0, 0], richtung: [0, 0, -1] });
  assert.equal(a.dn, undefined, 'DN nur aus der Unterlage');
  assert.equal(T.anschlussNeu({ name: 'e', art: 'elektro' }).form, undefined);
  const vl = testFamilie.anschluesse[0];
  assert.deepEqual(T.anschlussMitFeld(vl, 'lage.0', 'laenge - 60').anschluss.lage, ['laenge - 60', 'tiefe / 2', 'bodenabstand - stutzen']);
  assert.deepEqual(T.anschlussMitFeld(vl, 'richtung.2', '-1').anschluss.richtung, [0, 0, -1]);
  assert.ok(T.anschlussMitFeld(vl, 'lage.1', '').fehler);
  assert.equal(T.anschlussMitFeld(vl, 'dn', '20').anschluss.dn, 20);
  assert.equal(T.anschlussMitFeld(vl, 'dn', 'dn').anschluss.dn, 'dn', 'Formel');
  assert.equal(T.anschlussMitFeld(vl, 'dn', '').anschluss.dn, undefined);
  assert.equal(T.anschlussMitFeld(vl, 'gewinde', 'Rp 1/2').anschluss.gewinde, 'Rp 1/2');
  assert.equal(T.anschlussMitFeld({ ...vl, gewinde: 'x' }, 'gewinde', '').anschluss.gewinde, undefined);
  const kanal = T.anschlussMitFeld(vl, 'art', 'kanal').anschluss;
  assert.equal(kanal.art, 'kanal'); assert.equal(kanal.dn, undefined, 'dn gilt nur für Rohre'); assert.equal(kanal.form, 'rund');
  const rechteck = T.anschlussMitFeld({ ...kanal, d: 125 }, 'form', 'rechteck').anschluss;
  assert.equal(rechteck.d, undefined, 'd gilt nur für runde Kanäle');
  const el = T.anschlussMitFeld(vl, 'art', 'elektro').anschluss;
  assert.equal(el.form, undefined);
  assert.equal(vl.art, 'rohr', 'Original unverändert');
});

test('Medium-Auswahl: «*», Katalog, Parameter vom Datentyp medium', () => {
  const medien = [{ kennung: 'HZ-VL', name: 'Heizung Vorlauf' }, { kennung: 'KW', name: 'Trinkwasser kalt' }];
  const d = { parameter: [{ name: 'm1', datentyp: 'medium' }, { name: 'x', datentyp: 'laenge' }] };
  assert.deepEqual(T.mediumAuswahl(medien, d).map(([v]) => v), ['*', 'HZ-VL', 'KW', 'm1']);
  assert.deepEqual(T.mediumAuswahl(undefined, undefined).map(([v]) => v), ['*']);
});

// ================================================================================================ Fehler des Kerns

const kernAblehnung = {
  fehler: 'Familie «Testheizkörper Typ 22 (fiktiv)»: 2 Fehler – (1) parameter «oberkante».formel: Formel «bodenabstand + hoehe2»: Stelle 16: unbekannter Name «hoehe2» (2) koerper «platten».bis[3]: Division durch 0',
  vorschlag: '(1) Gemeint: hoehe? Parameter: hoehe, laenge (2) Nenner prüfen',
};

test('Fehler des Kerns: einzeln mit Stelle, Meldung und Vorschlag; Text ohne Vorspann', () => {
  const k = T.kernFehler(kernAblehnung);
  assert.equal(k.eintraege.length, 2);
  assert.equal(k.eintraege[0].stelle, 'parameter «oberkante».formel');
  assert.match(k.eintraege[0].meldung, /unbekannter Name «hoehe2»/);
  assert.match(k.eintraege[0].vorschlag, /Gemeint: hoehe\?/);
  assert.equal(k.eintraege[1].stelle, 'koerper «platten».bis[3]');
  assert.equal(k.eintraege[1].vorschlag, 'Nenner prüfen');
  assert.ok(!k.text.includes('Familie «'), 'ohne Vorspann');
  assert.match(k.text, /unbekannter Name/);
  const einfach = T.kernFehler({ fehler: 'Typ «x» gibt es in der Familie FAM-1 nicht.', vorschlag: 'Typen: a, b' });
  assert.equal(einfach.eintraege.length, 1);
  assert.equal(einfach.text, 'Typ «x» gibt es in der Familie FAM-1 nicht.');
  assert.equal(einfach.vorschlag, 'Typen: a, b');
  assert.equal(T.kernFehler({}).text, 'Abgelehnt.');
});

test('Sprungziel einer Fehlerstelle: Seite, Name, Feld', () => {
  assert.deepEqual(T.stelleZiel('parameter «oberkante».formel'), { tab: 'parameter', name: 'oberkante', feld: 'eingabe' });
  assert.deepEqual(T.stelleZiel('parameter «hoehe»'), { tab: 'parameter', name: 'hoehe', feld: '' });
  assert.deepEqual(T.stelleZiel('typ «22-600-1000».werte.hoehe'), { tab: 'typen', name: '22-600-1000', feld: 'hoehe' });
  assert.deepEqual(T.stelleZiel('koerper «platten».bis[3]'), { tab: 'koerper', name: 'platten', feld: 'bis' });
  assert.deepEqual(T.stelleZiel('koerper «r1».achse.punkt'), { tab: 'koerper', name: 'r1', feld: 'achse.punkt' });
  assert.deepEqual(T.stelleZiel('anschluss «vl».lage[1]'), { tab: 'anschluesse', name: 'vl', feld: 'lage' });
  assert.deepEqual(T.stelleZiel('sinnbild[2].radius'), { tab: 'sinnbild', name: '2', feld: 'radius' });
  assert.equal(T.stelleZiel('masse «L»'), null);
  assert.equal(T.stelleZiel(''), null);
});

test('Prüfliste: Definitionsfehler und Fehler je Typ aus der Abfrage familie', () => {
  const f = {
    definitionsfehler: [{ stelle: 'anschluss «vl».dn', meldung: 'unbekannter Name «x»', vorschlag: 'Gemeint: dn?' }],
    typen: [{ name: 'Standard', auswertungsfehler: [{ stelle: 'typ «Standard».werte.hoehe', meldung: 'kein Wert für «hoehe»', vorschlag: null }] }, { name: 'B', auswertungsfehler: [] }],
  };
  const z = T.pruefZeilen(f);
  assert.equal(z.length, 2);
  assert.equal(z[0].art, 'Definition'); assert.deepEqual(z[0].ziel, { tab: 'anschluesse', name: 'vl', feld: 'dn' });
  assert.equal(z[1].art, 'Typ «Standard»'); assert.deepEqual(z[1].ziel, { tab: 'typen', name: 'Standard', feld: 'hoehe' }); assert.equal(z[1].vorschlag, '');
  assert.deepEqual(T.pruefZeilen({ typen: [] }), []);
  assert.deepEqual(T.pruefZeilen(null), []);
  // fehlt ein Typwert, ist die Zelle des Typkatalogs das Ziel (die Stelle des Kerns nennt nur die Formel, die den Wert braucht)
  const fehlt = T.pruefZeilen({ typen: [{ name: 'Standard', auswertungsfehler: [{ stelle: 'parameter «oberkante».formel', meldung: 'Formel «bodenabstand + hoehe»: Stelle 16: Parameter «hoehe» hat keinen Wert', vorschlag: 'Wert im Typ angeben.' }] }] });
  assert.deepEqual(fehlt[0].ziel, { tab: 'typen', name: 'Standard', feld: 'hoehe' });
  // ohne erkennbare Stelle springt ein Typfehler wenigstens zum Typ
  assert.deepEqual(T.pruefZeilen({ typen: [{ name: 'T', auswertungsfehler: [{ stelle: '?', meldung: 'm' }] }] })[0].ziel, { tab: 'typen', name: 'T', feld: '' });
});

// ================================================================================================ Neue Familie, Laden

test('Neue Familie: nur familienfähige Klassen, minimaler Entwurf mit dem Typ «Standard»', () => {
  const klassen = [{ name: 'Wand', eigenschaften: ['dicke'] }, { name: 'Heizkoerper', eigenschaften: ['leistung', 'familientyp', 'parameter', 'drehung'] },
    { name: 'Pumpe', eigenschaften: ['familientyp *'] }, { name: 'Raum', eigenschaften: ['nummer *'] }];
  assert.deepEqual(T.familienKategorien(klassen), ['Heizkoerper', 'Pumpe']);
  assert.deepEqual(T.familienKategorien(undefined), []);
  const d = T.familieEntwurfMinimal('Heizkoerper', ' Mein Heizkörper ');
  assert.deepEqual(d, { format: 1, name: 'Mein Heizkörper', kategorie: 'Heizkoerper', parameter: [], typen: [{ name: 'Standard', werte: {} }], koerper: [], anschluesse: [] });
  assert.equal(T.neueFamiliePruefen({ kategorie: '', name: 'x' }, []), 'Kategorie wählen.');
  assert.equal(T.neueFamiliePruefen({ kategorie: 'Pumpe', name: ' ' }, []), 'Name fehlt.');
  assert.match(T.neueFamiliePruefen({ kategorie: 'Pumpe', name: 'abc' }, ['ABC']), /gibt es schon/);
  assert.equal(T.neueFamiliePruefen({ kategorie: 'Pumpe', name: 'abc' }, ['x']), true);
});

test('Familie laden: Pfad Pflicht; Dateien der Bibliothek aus dem Vorschlag des Kerns', () => {
  assert.equal(T.ladenPruefen(' '), 'Datei oder Pfad angeben.');
  assert.equal(T.ladenPruefen('a.familie.json'), true);
  assert.deepEqual(T.bibliotheksDateien('Bibliothek kataloge/familien: a.familie.json, b.familie.json'), ['a.familie.json', 'b.familie.json']);
  assert.deepEqual(T.bibliotheksDateien('Vollen Pfad einer .familie.json angeben (die Bibliothek kataloge/familien ist leer).'), []);
  assert.deepEqual(T.bibliotheksDateien(null), []);
});

// ================================================================================================ Bausteine

test('HTML-Bausteine: maskieren, Schlüssel für den Fokus, Formelfelder', () => {
  const f = E.eingabeFeld('a "b" <c>', { fk: 'p:x:name', feld: 'name', live: true, klasse: 'formel' });
  assert.match(f, /data-fk="p:x:name"/); assert.match(f, /data-feld="name"/); assert.match(f, /data-live/);
  assert.match(f, /value="a &quot;b&quot; &lt;c&gt;"/);
  const s = E.auswahlFeld([['a', 'A'], ['b', 'B']], 'c', { fk: 'x', feld: 'f', leer: '–' });
  assert.match(s, /<option value="">–<\/option>/); assert.match(s, /<option value="c" selected>c \(\?\)<\/option>/, 'unbekannter Wert bleibt sichtbar');
  assert.match(E.haken(true, { fk: 'h' }), /checked/);
  assert.match(E.knopf('Neu', 'parameter-neu', { deaktiviert: true }), /disabled/);
  assert.match(E.tabelle(['A', { html: 'B <i>x</i>', attr: { title: 't' } }], ['<tr><td>1</td></tr>']), /<th title="t">B <i>x<\/i><\/th>/);
});

// ================================================================================================ Ansicht: Projektion, Achsen, Rahmen

test('Projektion: u nach rechts, v nach oben wie die Ansichten des Kerns', () => {
  const p = [10, 20, 30];
  assert.deepEqual(A.projektion('vorne', p), [10, 30]);
  assert.deepEqual(A.projektion('hinten', p), [-10, 30]);
  assert.deepEqual(A.projektion('links', p), [-20, 30]);
  assert.deepEqual(A.projektion('rechts', p), [20, 30]);
  assert.deepEqual(A.projektion('oben', p), [10, 20]);
  assert.deepEqual(A.projektion('unten', p), [-10, 20]);
  assert.deepEqual(A.projektion('3d', p), [10, 30], '3D gilt nicht als Plan');
});

test('Achsen: in der Ansicht, auf die der Betrachter blickt, entfällt die Achse', () => {
  assert.deepEqual(A.achsenRichtungen('vorne'), { x: [1, 0], y: null, z: [0, 1] });
  assert.deepEqual(A.achsenRichtungen('oben'), { x: [1, 0], y: [0, 1], z: null });
  assert.deepEqual(A.achsenRichtungen('links'), { x: null, y: [-1, 0], z: [0, 1] });
  assert.equal(A.achsenLaenge([0, 0, 1000, 100]), 100);
  assert.equal(A.achsenLaenge([0, 0, 100, 100]), 50);
  assert.equal(A.achsenLaenge(null), 100);
});

test('Rahmen eines Körperteils: Quader, Zylinder, Kugel, Netz', () => {
  assert.deepEqual(A.teilBox({ art: 'quader', mitte: [500, 50, 450], masse: [1000, 100, 600] }), { min: [0, 0, 150], max: [1000, 100, 750] });
  const z = A.teilBox({ art: 'zylinder', von: [950, 50, 150], nach: [950, 50, 120], radius: 10 });
  assert.deepEqual(z.min.map(Math.round), [940, 40, 120]); assert.deepEqual(z.max.map(Math.round), [960, 60, 150]);
  assert.deepEqual(A.teilBox({ art: 'kugel', mitte: [0, 0, 50], radius: 50 }), { min: [-50, -50, 0], max: [50, 50, 100] });
  assert.deepEqual(A.teilBox({ art: 'koerper', ecken: [[0, 0, 0], [10, 20, 30], [5, 5, 5]], dreiecke: [] }), { min: [0, 0, 0], max: [10, 20, 30] });
  assert.equal(A.teilBox({ art: 'prisma' }), null);
  assert.equal(A.teilBox(null), null);
  const r = A.boxProjektion('vorne', { min: [0, 0, 150], max: [1000, 100, 750] });
  assert.deepEqual(r, { minu: 0, minv: 150, maxu: 1000, maxv: 750 });
  assert.deepEqual(A.boxProjektion('oben', { min: [0, 0, 150], max: [1000, 100, 750] }), { minu: 0, minv: 0, maxu: 1000, maxv: 100 });
  assert.deepEqual(A.boxProjektion('links', { min: [0, 0, 150], max: [1000, 100, 750] }), { minu: -100, minv: 150, maxu: 0, maxv: 750 });
  assert.equal(A.boxProjektion('vorne', null), null);
});

test('Zuordnung Körperliste ↔ Körperteile: gleiche Reihenfolge, nur Körper im Detailgrad; sonst nicht eindeutig', () => {
  const d = testFamilie;
  assert.deepEqual(A.teilZuKoerper(d, 'fein', 4), ['platten', 'abdeckung', 'stutzen_vl', 'stutzen_rl']);
  assert.deepEqual(A.teilZuKoerper(d, 'mittel', 2), ['platten', 'abdeckung']);
  assert.deepEqual(A.teilZuKoerper(d, 'grob', 1), ['platten']);
  assert.equal(A.teilZuKoerper(d, 'fein', 3), null, 'ein Körper ist durch «sichtbar» ausgeblendet: nicht eindeutig');
  assert.equal(A.teilIndexVon(d, 'fein', 4, 'stutzen_vl'), 2);
  assert.equal(A.teilIndexVon(d, 'fein', 4, 'gibtsnicht'), -1);
  assert.equal(A.teilIndexVon(d, 'fein', 3, 'platten'), -1);
  // ein Körper mit sichtbar = 0 zählt nicht mit
  const e = klon(d); e.koerper[1].sichtbar = 0;
  assert.deepEqual(A.teilZuKoerper(e, 'fein', 3), ['platten', 'stutzen_vl', 'stutzen_rl']);
});

test('Überlagerung: Ursprung, Achsen mit Buchstaben, Rahmen des gewählten Körpers', () => {
  const s = A.ueberlagerungSvg('vorne', { laenge: 100, rahmen: { minu: 0, minv: 150, maxu: 1000, maxv: 750 }, s: 1 });
  assert.match(s, /class="fe-rahmen" x="0" y="150" width="1000" height="600"/);
  assert.match(s, /class="fe-achse-x"/); assert.match(s, /class="fe-achse-z"/);
  assert.ok(!s.includes('fe-achse-y'), 'y zeigt auf den Betrachter');
  assert.match(s, /class="fe-ursprung"/); assert.match(s, />Ursprung \(Einfügepunkt\)</);
  assert.ok(!A.ueberlagerungSvg('oben', {}).includes('fe-rahmen'));
  assert.match(A.ueberlagerungSvg('oben', {}), /fe-achse-y/);
});

test('Sinnbild-Vorschau: nur Zahlen werden gezeichnet, Formeln rechnet nur der Kern', () => {
  assert.equal(A.sinnbildPrimitive({ art: 'linie', punkte: [[0, 0], ['laenge', 0]] }), null);
  assert.deepEqual(A.sinnbildPrimitive({ art: 'linie', punkte: [[0, 0], [100, 0]] }).punkte, [[0, 0], [100, 0]]);
  assert.equal(A.sinnbildPrimitive({ art: 'kreis', punkte: [[5, 5]], radius: 10 }).radius, 10);
  assert.equal(A.sinnbildPrimitive({ art: 'kreis', punkte: [[5, 5]], radius: 'r' }), null);
  assert.equal(A.sinnbildPrimitive({ art: 'polygon', punkte: [[0, 0], [1, 0]] }), null, 'zu wenig Punkte');
  assert.equal(A.sinnbildPrimitive({ art: 'text', punkte: [[0, 0]], text: 'T' }).text, 'T');
  assert.equal(A.sinnbildPrimitive({ art: 'linie', punkte: [[0, 0], [1, 1]], stil: { stift: 'dick' } }).stil.stift, 'dick');
  assert.equal(A.sinnbildPrimitive(null), null);
});

// ================================================================================================ Platzieren: Drehung und Vorschau

test('Drehung: 0 … < 360, Leertaste dreht um 90° (Umschalt zurück)', () => {
  assert.equal(A.drehungNormieren(450), 90);
  assert.equal(A.drehungNormieren(-90), 270);
  assert.equal(A.drehungNormieren(360), 0);
  assert.equal(A.drehungNormieren('x'), 0);
  assert.equal(A.drehungNormieren(22.5), 22.5);
  assert.equal(A.drehungWeiter(0), 90); assert.equal(A.drehungWeiter(270), 0); assert.equal(A.drehungWeiter(0, true), 270);
  let d = 0; for (let i = 0; i < 4; i++) d = A.drehungWeiter(d);
  assert.equal(d, 0, 'vier Leertasten = eine volle Drehung');
});

test('Weltlage nach Handrechnung: Einfügepunkt [5000, 3000], Typ 22-600-1000, Drehung 90° → vl (4950, 3950), rl (4950, 3900)', () => {
  // Familienkoordinaten: vl (laenge − randabstand, tiefe / 2) = (950, 50), rl (laenge − randabstand − anschlussabstand, tiefe / 2) = (900, 50)
  const vl = A.weltPunkt([1000 - 50, 100 / 2], 90, [5000, 3000]);
  const rl = A.weltPunkt([1000 - 50 - 50, 100 / 2], 90, [5000, 3000]);
  nah(vl[0], 4950); nah(vl[1], 3950); nah(rl[0], 4950); nah(rl[1], 3900);
  const null0 = A.weltPunkt([950, 50], 0, [5000, 3000]);
  nah(null0[0], 5950); nah(null0[1], 3050);
  const halb = A.weltPunkt([950, 50], 180, [5000, 3000]);
  nah(halb[0], 4050); nah(halb[1], 2950);
});

test('Vorschau am Mauszeiger: Umriss von oben gedreht und verschoben, Pfeil zur Vorderseite', () => {
  const antwort = { plangrafik: { teile: [
    { id: 'x', ebene: 'apparat', primitive: [{ art: 'linie', punkte: [[0, 0], [1000, 0]] }, { art: 'polylinie', punkte: [[1000, 0], [1000, 100]] }, { art: 'text', punkte: [[0, 0]], text: 'T' }] },
    { ebene: 'symbol', primitive: [{ art: 'kreis', punkte: [[950, 50]], radius: 6 }] },
  ] } };
  const prim = A.umrissPrimitive(antwort);
  assert.equal(prim.length, 2, 'Text und Anschlussmarken entfallen');
  const r0 = A.vorschauRahmen(prim, 0, [5000, 3000]);
  assert.deepEqual(r0, { minx: 5000, miny: 3000, maxx: 6000, maxy: 3100 });
  const r90 = A.vorschauRahmen(prim, 90, [5000, 3000]);
  nah(r90.minx, 4900); nah(r90.maxx, 5000); nah(r90.miny, 3000); nah(r90.maxy, 4000);
  const svg = A.vorschauSvg(prim, 90, [5000, 3000], 1);
  assert.match(svg, /<polyline class="wz-vorschau wz-offen"/);
  assert.match(svg, /class="wz-oeffnung"/, 'Pfeil zur Vorderseite');
  assert.equal(A.vorschauSvg(prim, 0, null, 1), '');
  assert.match(A.vorschauSvg([{ art: 'polygon', punkte: [[0, 0], [10, 0], [10, 10]] }, { art: 'kreis', punkte: [[5, 5]], radius: 3 }, { art: 'bogen', punkte: [[0, 0]], radius: 10, von: 0, bis: 90 }], 0, [0, 0]), /<polygon class="wz-vorschau"[\s\S]*<circle[\s\S]*<polyline/);
  assert.equal(A.vorschauRahmen([], 0, [0, 0]), null);
});

// ================================================================================================ Typauswahl

const liste = [
  { kennzeichen: 'FAM-2', name: 'Wärmezähler', kategorie: 'Waermezaehler', status: 'entwurf', version: 1, typen: ['DN15', 'DN20'], gueltig: true },
  { kennzeichen: 'FAM-1', name: 'Testheizkörper', kategorie: 'Heizkoerper', status: 'freigegeben', version: 3, typen: ['22-600-1000', '22-600-1400'], gueltig: true, verwendungen: 2, definitionsfehler: 0 },
  { kennzeichen: 'FAM-3', name: 'Kaputt', kategorie: 'Pumpe', status: 'entwurf', version: 1, typen: ['A'], gueltig: false, definitionsfehler: 2 },
  { kennzeichen: 'FAM-4', name: 'Ohne Typ', kategorie: 'Pumpe', status: 'entwurf', version: 1, typen: [], gueltig: true },
];

test('Typauswahl «Familie: Typ»: nur gültige Familien mit Typen, sortiert nach Namen', () => {
  const o = Q.typOptionen(liste);
  assert.deepEqual(o, [['FAM-1|22-600-1000', 'Testheizkörper: 22-600-1000'], ['FAM-1|22-600-1400', 'Testheizkörper: 22-600-1400'], ['FAM-2|DN15', 'Wärmezähler: DN15'], ['FAM-2|DN20', 'Wärmezähler: DN20']]);
  assert.deepEqual(Q.typTeile('FAM-1|22-600-1000'), { familie: 'FAM-1', typ: '22-600-1000' });
  assert.deepEqual(Q.typTeile('FAM-1|a|b'), { familie: 'FAM-1', typ: 'a|b' });
  assert.equal(Q.typTeile(''), null); assert.equal(Q.typTeile('FAM-1'), null); assert.equal(Q.typTeile('FAM-1|'), null); assert.equal(Q.typTeile(null), null);
  assert.equal(Q.typWert('FAM-1', 'x'), 'FAM-1|x');
  assert.deepEqual(Q.typOptionen([]), []);
});

const famAntwort = {
  kennzeichen: 'FAM-1', name: 'Testheizkörper', kategorie: 'Heizkoerper', status: 'entwurf', version: 3, gueltig: true, definitionsfehler: [], verwendungen: ['HK-1'],
  definition: testFamilie,
  typen: [{ name: '22-600-1000', gueltig: true, werte: { hoehe: 600, laenge: 1000, bodenabstand: 150, oberkante: 750, mit_abdeckung: 1, dn: 15 } },
    { name: '22-900-800', gueltig: true, werte: { hoehe: 900, laenge: 800, bodenabstand: 150, oberkante: 1050, mit_abdeckung: 0, dn: 15 } }],
};

test('Felder der Optionsleiste: Typ, Drehung, dann die Exemplarparameter der gewählten Familie (ohne Formelparameter)', () => {
  const ohne = Q.familieFelder('', null, liste);
  assert.deepEqual(ohne.map((f) => f.id), ['typ', 'drehung']);
  assert.equal(ohne[1].einheit, '°'); assert.equal(ohne[1].vorgabe, 0);
  assert.deepEqual(ohne[0].werte.map(([v]) => v), ['FAM-1|22-600-1000', 'FAM-1|22-600-1400', 'FAM-2|DN15', 'FAM-2|DN20']);
  const mit = Q.familieFelder('FAM-1|22-600-1000', famAntwort, liste);
  assert.deepEqual(mit.map((f) => f.id), ['typ', 'drehung', 'p_bodenabstand'], 'oberkante hat eine Formel und ist nicht einstellbar');
  const b = mit[2];
  assert.equal(b.art, 'zahl'); assert.equal(b.einheit, 'mm'); assert.equal(b.fluechtig, true); assert.equal(b.param, 'bodenabstand');
  // gewählte Familie passt nicht zur geladenen Definition: keine Exemplarfelder
  assert.deepEqual(Q.familieFelder('FAM-2|DN15', famAntwort, liste).map((f) => f.id), ['typ', 'drehung']);
  // alle Felder genügen den Regeln von optionen.js (Art, eindeutige Ids)
  for (const f of mit) assert.ok(['zahl', 'text', 'auswahl', 'bool'].includes(f.art));
  assert.equal(new Set(mit.map((f) => f.id)).size, mit.length);
  const ja = Q.exemplarFeldDefs({ definition: { parameter: [{ name: 'a', art: 'exemplar', datentyp: 'ja_nein' }, { name: 'b', art: 'exemplar', datentyp: 'text' }, { name: 'c', art: 'exemplar', datentyp: 'winkel' }, { name: 'd', art: 'typ', datentyp: 'laenge' }] } });
  assert.deepEqual(ja.map((f) => [f.id, f.art]), [['p_a', 'auswahl'], ['p_b', 'text'], ['p_c', 'zahl']]);
  assert.equal(ja[0].leer, '(Typ)'); assert.equal(ja[2].einheit, '°');
});

test('Exemplarwerte aus der Optionsleiste: nur ausgefüllte Felder, ja/nein → Wahrheitswert; Parameter für familie_platzieren', () => {
  const def = { definition: { parameter: [{ name: 'bodenabstand', art: 'exemplar', datentyp: 'laenge' }, { name: 'sichtbar', art: 'exemplar', datentyp: 'ja_nein' }, { name: 'bem', art: 'exemplar', datentyp: 'text' }] } };
  assert.deepEqual(Q.exemplarWerte(def, { p_bodenabstand: null, p_sichtbar: '', p_bem: '' }), {});
  assert.deepEqual(Q.exemplarWerte(def, { p_bodenabstand: 200, p_sichtbar: 'nein', p_bem: 'x' }), { bodenabstand: 200, sichtbar: false, bem: 'x' });
  assert.deepEqual(Q.exemplarWerte(def, { p_bodenabstand: '250,5', p_sichtbar: 'ja' }), { bodenabstand: 250.5, sichtbar: true });
  assert.deepEqual(Q.exemplarWerte(def, { p_bodenabstand: 'abc' }), {});
  const p = Q.platzierParameter({ typ: 'FAM-1|22-600-1000', drehung: 90, p_bodenabstand: 200 }, [5000.123, 3000], famAntwort, 'EG');
  assert.deepEqual(p, { familie: 'FAM-1', typ: '22-600-1000', punkt: [5000.12, 3000], drehung: 90, parameter: { bodenabstand: 200 }, geschoss: 'EG' });
  assert.deepEqual(Q.platzierParameter({ typ: 'FAM-1|T', drehung: null }, [1, 2], null, null), { familie: 'FAM-1', typ: 'T', punkt: [1, 2], drehung: 0 });
  assert.equal(Q.platzierParameter({ typ: '' }, [1, 2], null), null);
});

// ================================================================================================ Projektbrowser

test('Projektbrowser: Familien je Kategorie mit Typen; Entwurf grau mit Marke, Fehler markiert, Katalog als eigener Knoten', () => {
  const katalog = { key: 'f:katalog', label: 'Katalog', kinder: [], art: 'ast' };
  const ast = B.familienAst(liste, katalog, (kz) => `id-${kz}`);
  assert.equal(ast.key, 'w:familien'); assert.equal(ast.label, 'Familien'); assert.equal(ast.zahl, 4);
  assert.deepEqual(ast.kinder.map((k) => k.label), ['Heizkoerper', 'Pumpe', 'Waermezaehler', 'Katalog'], 'Kategorien alphabetisch, am Ende der Katalog');
  const heiz = ast.kinder[0];
  assert.equal(heiz.zahl, 1);
  const f = heiz.kinder[0];
  assert.equal(f.art, 'fam'); assert.equal(f.kz, 'FAM-1'); assert.equal(f.id, 'id-FAM-1'); assert.equal(f.marke, ''); assert.equal(f.css, '');
  assert.deepEqual(f.kinder.map((t) => [t.art, t.label, t.kz, t.typ]), [['fam-typ', '22-600-1000', 'FAM-1', '22-600-1000'], ['fam-typ', '22-600-1400', 'FAM-1', '22-600-1400']]);
  assert.match(f.tipp, /Version 3/); assert.match(f.tipp, /2× platziert/);
  const pumpe = ast.kinder[1].kinder;
  assert.deepEqual(pumpe.map((x) => x.label), ['Kaputt', 'Ohne Typ'], 'Familien nach Namen');
  assert.equal(pumpe[1].marke, 'Entwurf'); assert.match(pumpe[1].css, /entwurf/);
  const kaputt = pumpe.find((x) => x.kz === 'FAM-3');
  assert.equal(kaputt.marke, 'Fehler'); assert.match(kaputt.css, /fehlerhaft/);
  assert.equal(B.familienAst([], null).kinder.length, 0);
  assert.equal(B.familienAst([], null).zahl, 0);
  assert.equal(B.familienKnoten(liste[0]).id, undefined);
});

// ================================================================================================ Palette

const H = { zeile: (l, f, o = {}) => `<z l="${l}"${o.klasse ? ` k="${o.klasse}"` : ''}>${f}</z>`, gruppe: (n, z) => `<g n="${n}">${z.join('')}</g>`, nurLesen: (t) => `<t>${t}</t>`, symbol: '<s/>' };
const lagen = { anschluesse: [{ name: 'vl', medium: 'HZ-VL', fluss: 'rein', dn: 15, lage: [4950, 3950, 120], richtung: [0, 0, -1], verbunden: null }, { name: 'rl', medium: 'HZ-RL', fluss: 'raus', dn: 15, lage: [4950, 3900, 120.004], richtung: [0, 0, -1], verbunden: 'RO-1.a' }] };

test('Palette eines platzierten Elements: Familie mit Editor-Knopf, Typ als Auswahl, Exemplarparameter, Anschlüsse mit Weltlage', () => {
  const h = P.elementGruppen({ f: famAntwort, lagen, typ: '22-600-1000', exemplar: { bodenabstand: 200 }, kz: 'HK-1' }, H);
  assert.match(h, /<g n="Familie">/);
  assert.match(h, /data-aktion="fam-editor" data-fam="FAM-1"/);
  assert.match(h, /<select data-ziel="eig:familientyp"[^>]*><option value="22-600-1000" selected>22-600-1000<\/option><option value="22-900-800">22-900-800<\/option>/);
  assert.match(h, /data-ziel="fam:param:bodenabstand"[^>]*value="200"/);
  assert.match(h, /placeholder="150"/, 'der Wert des Typs steht als Platzhalter');
  assert.match(h, /<z l="oberkante"><span class="ep-text mono"[^>]*>= bodenabstand \+ hoehe/, 'Formelparameter nur lesend');
  assert.ok(!h.includes('data-ziel="fam:param:oberkante"'));
  assert.match(h, /<g n="Anschlüsse">/);
  assert.match(h, /HZ-VL · rein · DN 15/);
  assert.match(h, /4950 · 3950 · 120/);
  assert.match(h, /4950 · 3900 · 120(?![.\d])/, 'Lage auf 0.01 gerundet');
  assert.match(h, /→ RO-1\.a/);
  assert.equal(P.elementGruppen({ f: null }, H), '');
  assert.ok(!P.elementGruppen({ f: famAntwort, lagen: null, typ: 'x' }, H).includes('Anschlüsse'));
  assert.match(P.elementGruppen({ f: famAntwort, lagen: null, typ: 'alt' }, H), /alt \(gibt es nicht mehr\)/);
});

test('Palette: ja/nein als Ankreuzfeld, Exemplarwerte ändern → neues Objekt für element_aendern', () => {
  const f = { ...famAntwort, definition: { parameter: [{ name: 'ab', art: 'exemplar', datentyp: 'ja_nein' }, { name: 'h', art: 'exemplar', datentyp: 'laenge' }, { name: 't', art: 'exemplar', datentyp: 'text' }] }, typen: [{ name: 'A', werte: { ab: 1, h: 100 } }] };
  const html = P.elementGruppen({ f, lagen: null, typ: 'A', exemplar: null }, H);
  assert.match(html, /data-typ="famja" checked/, 'Vorgabe des Typs = ja');
  const ab = f.definition.parameter[0], h = f.definition.parameter[1], t = f.definition.parameter[2];
  assert.deepEqual(P.exemplarNeu(null, h, '250'), { objekt: { h: 250 } });
  assert.deepEqual(P.exemplarNeu({ h: 250 }, ab, false), { objekt: { h: 250, ab: false } });
  assert.deepEqual(P.exemplarNeu({ h: 250 }, h, ''), { objekt: null }, 'alles leer: Eigenschaft entfernen');
  assert.deepEqual(P.exemplarNeu({ h: 250, t: 'x' }, h, ' '), { objekt: { t: 'x' } });
  assert.match(P.exemplarNeu(null, h, 'abc').fehler, /Zahl/);
  assert.deepEqual(P.exemplarNeu(null, t, 'Text'), { objekt: { t: 'Text' } });
  assert.deepEqual(P.exemplarNeu('kaputt', h, '1'), { objekt: { h: 1 } });
  assert.deepEqual(P.exemplarParameter(famAntwort).map((p) => p.name), ['bodenabstand']);
  assert.deepEqual(P.exemplarFormeln(famAntwort).map((p) => p.name), ['oberkante']);
  assert.equal(P.typVorgabe(famAntwort, '22-900-800', 'hoehe'), 900);
  assert.equal(P.typVorgabe(famAntwort, 'gibtsnicht', 'hoehe'), undefined);
});

test('Palette der Familie selbst: Kopf, Inhalt, Fehler; Kopfangaben als kopf_setzen', () => {
  const h = P.familienKopfHtml({ f: famAntwort }, H);
  assert.match(h, /Entwurf · Version 3 · 1 platzierte Elemente/);
  assert.match(h, /data-ziel="famkopf:name"[^>]*value="Testheizkörper Typ 22 \(fiktiv\)"/, 'Name aus der Definition');
  assert.match(h, /data-ziel="famkopf:hersteller"[^>]*value="keiner – Testfamilie"/);
  assert.match(h, /<z l="Kategorie"><t>Heizkoerper<\/t>/);
  assert.match(h, /<z l="Typen"><t>22-600-1000, 22-900-800<\/t>/);
  assert.match(h, /<z l="Verwendungen"><t>HK-1<\/t>/);
  assert.ok(!h.includes('Prüfung'));
  assert.match(P.familienKopfHtml({ f: { ...famAntwort, gueltig: false, definitionsfehler: [{ stelle: 's', meldung: 'm' }] } }, H), /<g n="Prüfung">[\s\S]*s: m/);
  assert.match(P.familienKopfHtml({ f: null }, H), /wird geladen/);
  assert.deepEqual(P.kopfOp('hersteller', ' Fa. X '), { op: { op: 'kopf_setzen', hersteller: 'Fa. X' } });
  assert.deepEqual(P.kopfOp('produkt', ''), { op: { op: 'kopf_setzen', produkt: null } }, 'leer entfernt');
  assert.match(P.kopfOp('name', ' ').fehler, /nicht leer/);
});

test('Marke «Familie» nur bei Herkunft familie', () => {
  assert.match(P.familienMarke('familie, T, 2026-10-18 16:05, Testheizkörper Typ 22 Typ 22-600-1000'), /ep-fam-marke/);
  assert.equal(P.familienMarke('mensch, T, 2026-10-18 16:05'), '');
  assert.equal(P.familienMarke(undefined), '');
});

// ================================================================================================ Verdrahtung

test('Verdrahtung: Zuordnung der Ansichtsart, Kontextregister Familie mit den Gruppen der Spezifikation, Symbole, Aktionen, Kürzel', () => {
  const fenster = lies('../fenster.js');
  assert.match(fenster, /const MODULE_ZUSATZ = \{\s*familie: '\.\/familien\/editor\.js'/);
  assert.match(fenster, /a\.klasse !== 'Familie'/);
  assert.ok(existsSync(path.resolve(hier, 'editor.js')));
  const mb = lies('../oberflaeche/menueband.js');
  const folge = /familie: \[([^\]]*)\]/.exec(mb)[1].match(/'([^']+)'/g).map((x) => x.slice(1, -1));
  assert.deepEqual(folge, ['Parameter', 'Typen', 'Körper', 'Anschlüsse', 'Sinnbild', 'Ansicht', 'Prüfen', 'Unterlagen', 'Bibliothek']);   // «Unterlagen» seit C2 (aktionen-unterlagen.js), «Bibliothek» seit C3 (aktionen-bibliothek.js)
  assert.match(mb, /data-register="familie">Familie</);
  const a = lies('aktionen-familien.js');
  assert.match(lies('../oberflaeche/aktionen.js'), /import '\.\.\/familien\/aktionen-familien\.js'/);
  const gruppen = [...a.matchAll(/gruppe: '([^']+)'/g)].map((m) => m[1]).filter((g) => folge.includes(g));
  assert.deepEqual([...new Set(gruppen)], folge.filter((g) => g !== 'Unterlagen' && g !== 'Bibliothek'), 'jede Gruppe hat Aktionen, in der Reihenfolge des Registers');   // Unterlagen (C2) und Bibliothek (C3) stehen in eigenen Dateien
  const unbekannt = sym.symbol('gibtsnicht');
  for (const m of a.matchAll(/symbol: '([^']+)'|, '(fam[A-Za-z]+)'\]|\['[a-z]+', '[^']+', '(fam[A-Za-z]+)'\]/g)) {
    const s = m[1] ?? m[2] ?? m[3];
    assert.notEqual(sym.symbol(s), unbekannt, `Symbol «${s}» fehlt in symbole.js`);
  }
  for (const s of ['famFamilie', 'famTyp', 'ansicht3d', 'oeffnen', 'neu']) assert.ok(s === 'famTyp' ? true : sym.symbol(s) !== unbekannt, s);
  assert.match(a, /kuerzel: 'CM'/);
  assert.match(a, /register: 'gebaeudetechnik', gruppe: 'Apparate und Armaturen'/);
  // Aktionen laufen nur über Befehle und Abfragen des Kerns
  for (const b of ['familie_entwurf']) assert.ok(a.includes(`'${b}'`), b);
  assert.ok(lies('bibliothek-dialoge.js').includes("'familie_laden'"), 'familie_laden (seit C3 im Dialog «Familie laden»)');
  const t = lies('platzieren.js');
  assert.ok(t.includes("'familie_platzieren'") && t.includes("'familie_ansicht'"));
  assert.ok(lies('editor.js').includes("'familie_aendern'"));
  assert.ok(lies('daten.js').includes("'familien'") && lies('daten.js').includes("'familie'"));
  assert.ok(lies('../oberflaeche/eigenschaften.js').includes("'anschlusslagen'"));
  // Der Editor ändert nur über familie_aendern, nie über element_aendern
  for (const datei of ['editor.js', 'editor-tabellen.js', 'editor-koerper.js']) assert.ok(!lies(datei).includes('element_aendern'), datei);
  // Projektbrowser
  // (G1: Zeichnen und Bedienen in projektbrowser.js, die Äste in projektbrowser-aeste.js, die Reihenfolge in der Tabelle projektbrowser-gliederung.js)
  const pb = lies('../oberflaeche/projektbrowser.js') + lies('../oberflaeche/projektbrowser-aeste.js');
  for (const s of ["'familie.bearbeiten'", "'familie.platzieren-aus-browser'", "'familie.neu'", "'familie.laden'", "kontextArt === 'familien'", "quelle: 'familien'", "'f:katalog', 'Katalog'"]) assert.ok(pb.includes(s), s);
  assert.match(lies('../oberflaeche/projektbrowser-gliederung.js'), /quelle: 'huelle' \},\s*\{ quelle: 'systeme' \}/);
  // Werkzeug: Eintrag in optionen.js
  const o = lies('../werkzeuge/optionen.js');
  assert.match(o, /familie: \{\s*titel: 'Familie platzieren',\s*get felder\(\)/);
  // eine eigene Datei CSS, keine festen Farben ausserhalb der Variablen-Definitionen, keine Schrift ausser 12/11/9 px, kein zoom
  const css = lies('../../css/familien.css').replace(/\/\*[\s\S]*?\*\//g, '');
  const ohneVar = css.replace(/:root(\[[^\]]*\])?\s*\{[^}]*\}/g, '');
  assert.deepEqual(ohneVar.match(/#[0-9a-fA-F]{3,8}\b/g) ?? [], [], 'feste Farben');
  for (const m of css.matchAll(/font-size:\s*([\d.]+)px/g)) assert.ok(['12', '11', '9'].includes(m[1]), `Schriftgrösse ${m[1]}`);
  assert.doesNotMatch(css, /(^|[;{\s])zoom\s*:/);
  assert.match(lies('../../index.html'), /href="css\/familien\.css"/);
});

test('Verdrahtung: die Parameter der Befehle, die die Oberfläche sendet, gibt es im Kern (Beschreibung der Befehle)', () => {
  const kern = path.resolve(hier, '../../../../Hlks.Kern/Familien/FamilienBefehle.cs');
  if (!existsSync(kern)) return;
  const cs = readFileSync(kern, 'utf8');
  for (const op of ['parameter_setzen', 'parameter_loeschen', 'typ_setzen', 'typ_loeschen', 'koerper_setzen', 'koerper_loeschen', 'anschluss_setzen', 'anschluss_loeschen', 'sinnbild_setzen', 'kopf_setzen'])
    assert.ok(cs.includes(`"${op}"`), `Kern kennt ${op}`);
  const gesendet = ['editor-tabellen.js', 'editor-koerper.js', 'familien-text.js', 'familien-palette.js'].map(lies).join('\n');
  for (const op of ['parameter_setzen', 'parameter_loeschen', 'typ_setzen', 'typ_loeschen', 'koerper_setzen', 'koerper_loeschen', 'anschluss_setzen', 'anschluss_loeschen', 'sinnbild_setzen', 'kopf_setzen'])
    assert.ok(gesendet.includes(`'${op}'`), `die Oberfläche benutzt ${op}`);
  for (const p of ['definition', 'familie', 'ops', 'datei', 'typ', 'punkt', 'drehung', 'parameter', 'geschoss']) assert.ok(cs.includes(`new("${p}"`), `Parameter ${p}`);
});

// ================================================================================================ Gegen den echten Kern (nur Trockenläufe und Abfragen)

test('Echter Kern (HLKS_TEST_SERVER): alle Platzhalter und ops der Oberfläche nimmt der Kern an', { skip: !process.env.HLKS_TEST_SERVER }, async () => {
  const s = process.env.HLKS_TEST_SERVER.replace(/\/$/, '');
  const post = async (art, name, body) => (await fetch(`${s}/api/${art}/${name}${art === 'befehl' ? '?trockenlauf=true' : ''}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();
  const mit = async (aenderung) => {
    const d = klon(testFamilie); delete d.id; d.name = 'Probe C1'; aenderung(d);
    return post('befehl', 'familie_entwurf', { definition: d });
  };
  // Ausgangslage: die Testfamilie ist gültig
  assert.equal((await mit(() => {})).angenommen, true);
  // jede Körperart mit Platzhaltermassen
  for (const [art] of T.KOERPER_ARTEN) {
    const r = await mit((d) => { d.koerper.push(T.koerperNeu(art, `${art}1`)); });
    assert.equal(r.angenommen, true, `${art}: ${r.fehler}`);
  }
  // jedes Sinnbildteil mit Platzhaltermassen
  for (const [art] of T.SINNBILD_ARTEN) {
    const r = await mit((d) => { d.sinnbild = [T.sinnbildNeu(art)]; });
    assert.equal(r.angenommen, true, `${art}: ${r.fehler}`);
  }
  // Anschluss mit leerer Grösse ist ein Hinweis, kein Fehler; Kanal rund und rechteckig, elektro
  for (const a of [{ art: 'rohr' }, { art: 'kanal', form: 'rund' }, { art: 'kanal', form: 'rechteck' }, { art: 'elektro' }]) {
    const r = await mit((d) => { d.anschluesse.push(T.anschlussNeu({ name: 'neu', medium: '*', fluss: 'beide', ...a })); });
    assert.equal(r.angenommen, true, `${a.art} ${a.form ?? ''}: ${r.fehler}`);
  }
  // Neue Familie: minimaler Entwurf
  const klassen = (await post('abfrage', 'katalog', {})).klassen;
  const kategorien = T.familienKategorien(klassen);
  assert.ok(kategorien.includes('Heizkoerper'), 'Heizkörper ist familienfähig');
  for (const k of kategorien) assert.equal((await post('befehl', 'familie_entwurf', { definition: T.familieEntwurfMinimal(k, `Probe ${k}`) })).angenommen, true, k);
  // Der Kern lehnt Fehler mit Stelle ab; die Oberfläche zerlegt sie
  const bad = await mit((d) => { d.parameter.find((p) => p.name === 'oberkante').formel = 'bodenabstand + hoehe2'; });
  assert.equal(bad.angenommen, false);
  const k = T.kernFehler(bad);
  assert.equal(k.eintraege[0].stelle, 'parameter «oberkante».formel');
  assert.match(k.eintraege[0].meldung, /unbekannter Name «hoehe2»/);
  assert.deepEqual(T.stelleZiel(k.eintraege[0].stelle), { tab: 'parameter', name: 'oberkante', feld: 'eingabe' });
  // Umbenennen: die ops ergeben eine gültige Definition (auf eine Kopie der Testfamilie angewendet)
  const u = T.umbenennenOps(testFamilie, 'hoehe', 'bauhoehe');
  const probe = klon(testFamilie); delete probe.id; probe.name = 'Probe C1 Umbenennen';
  // ops lokal anwenden (so wie der Kern: setzen ersetzt oder hängt an, löschen entfernt Parameter samt Typwerten)
  for (const op of u.ops) {
    const ersetzen = (liste, schluessel, neu) => { const i = liste.findIndex((x) => x[schluessel] === neu[schluessel]); if (i >= 0) liste[i] = neu; else liste.push(neu); };
    if (op.op === 'parameter_setzen') ersetzen(probe.parameter, 'name', op.parameter);
    else if (op.op === 'typ_setzen') ersetzen(probe.typen, 'name', op.typ);
    else if (op.op === 'koerper_setzen') ersetzen(probe.koerper, 'id', op.koerper);
    else if (op.op === 'anschluss_setzen') ersetzen(probe.anschluesse, 'name', op.anschluss);
    else if (op.op === 'sinnbild_setzen') { if (op.sinnbild === null) delete probe.sinnbild; else probe.sinnbild = op.sinnbild; }
    else if (op.op === 'parameter_loeschen') { probe.parameter = probe.parameter.filter((p) => p.name !== op.name); for (const t of probe.typen) delete t.werte[op.name]; }
  }
  const r = await post('befehl', 'familie_entwurf', { definition: probe });
  assert.equal(r.angenommen, true, r.fehler);
  // Die Abfrage familie liefert, was die Palette und die Typauswahl brauchen
  const liste2 = (await post('abfrage', 'familien', {})).familien;
  assert.ok(Array.isArray(liste2));
  assert.doesNotThrow(() => Q.typOptionen(liste2));
  assert.doesNotThrow(() => B.familienAst(liste2, null));
});
