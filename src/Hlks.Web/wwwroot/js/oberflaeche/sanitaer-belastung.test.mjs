// Tests der Oberfläche der Sanitär-Belastungswerte (Modul 4 Teil A-O) ohne Browser: Rundung und Formatierung (sanitaer-text.js), Auswahl der Apparateart und Palettenzeilen
// (sanitaer-palette.js), Tabelle der Spezialinstallationen (Eingabe → Wert für den Kern), aktive Anlage (sanitaer-anlage.js), die Listenansicht «Sanitär-Zusammenstellung (W3)»
// samt CSV (ansichten/sanitaer-liste.js, listenformat.js, liste-csv.js) mit dem Referenzfall Walkringen aus dem Katalog, Verdrahtung (Aktion, Symbole, Katalog, Stylesheet).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/sanitaer-belastung.test.mjs
// Zahlen: nur Katalogwerte aus kataloge/sanitaer_belastungswerte.json (Christians Excel «LU_DU Zusammenstellung_.xlsx») und fiktive Testdaten – nie Normwerte.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import * as T from './sanitaer-text.js';
import * as P from './sanitaer-palette.js';
import * as A from './sanitaer-anlage.js';
import * as L from '../ansichten/sanitaer-liste.js';
import { aufbereiten, formatWert } from '../ansichten/listenformat.js';
import * as CSV from '../ansichten/liste-csv.js';
import * as S from './symbole.js';
import * as RV from './symbole-revit.js';

const hier = path.dirname(fileURLToPath(import.meta.url));
const lies = (rel) => readFileSync(path.resolve(hier, rel), 'utf8');
const klassen = JSON.parse(lies('../../../../../kataloge/klassen.json'));
const klasse = (n) => (Array.isArray(klassen) ? klassen : klassen.klassen).find((k) => k.name === n);
const SKAT = JSON.parse(lies('../../../../../kataloge/sanitaer_belastungswerte.json'));

// ---------------------------------------------------------------------------------------------- Rundung und Formatierung

test('LU: ganze Zahl, sonst eine Nachkommastelle; Komma nur auf Wunsch', () => {
  assert.equal(T.formatLu(80), '80');
  assert.equal(T.formatLu(80.00000000000001), '80');
  assert.equal(T.formatLu(2.5), '2.5');
  assert.equal(T.formatLu(2.5, { komma: true }), '2,5');
  assert.equal(T.formatLu(0), '0');
  assert.equal(T.formatLu(null), '');
  assert.equal(T.formatLu(undefined), '');
  assert.equal(T.formatLu(NaN), '');
});

test('DU: immer eine Nachkommastelle; Fliesskommafehler der Summe verschwinden', () => {
  assert.equal(T.formatDu(0.5), '0.5');
  assert.equal(T.formatDu(2), '2.0');
  assert.equal(T.formatDu(38.800000000000004), '38.8');
  assert.equal(T.formatDu(38.8, { komma: true }), '38,8');
  assert.equal(T.formatDu(16.7, { komma: true }), '16,7');
  assert.equal(T.formatDu(0.15), '0.2');   // wie geschrieben gerundet (nicht 0.1 wegen 0.15 = 0.1499…)
  assert.equal(T.formatDu(null), '');
});

test('l/s: zwei Nachkommastellen, unter 1 l/s drei – Referenzwerte Walkringen', () => {
  assert.equal(T.formatLs(SKAT.referenz.spitzeKurve3, { komma: true }), '1,14');   // 1.1412355278976094
  assert.equal(T.formatLs(SKAT.referenz.spitzeKurve5, { komma: true }), '1,16');   // 1.160616349950851
  assert.equal(T.formatLs(SKAT.referenz.schmutzwasser, { komma: true }), '3,11');  // 3.1144823004794873
  assert.equal(T.formatLs(SKAT.referenz.volumenstrom, { komma: true }), '13,20');
  assert.equal(T.formatLs(0.7617626677197794, { komma: true }), '0,762');
  assert.equal(T.formatLs(0.5), '0.500');
  assert.equal(T.formatLs(0), '0.00');
  assert.equal(T.formatLs(null), '');
});

test('Eingabewerte (Spezialinstallationen): wie eingegeben, höchstens drei Stellen ohne Nachnullen', () => {
  assert.equal(T.formatEingabe(0.5, { komma: true }), '0,5');
  assert.equal(T.formatEingabe(0.25), '0.25');
  assert.equal(T.formatEingabe(2), '2');
  assert.equal(T.formatEingabe(0.1 + 0.2), '0.3');
  assert.equal(T.formatEingabe(null), '');
});

test('Stückzahl je Geschoss: 0 bleibt leer wie die leere Excel-Zelle', () => {
  assert.equal(T.formatStueck(0), '');
  assert.equal(T.formatStueck(2), '2');
  assert.equal(T.formatStueck(null), '');
  assert.equal(T.genau(0.7617626677197794, 'l/s'), '0,7617626677197794 l/s');
});

test('die Rechenkonstanten und Formeln stehen nicht in der Oberfläche (nur im Kern und im Katalog)', () => {
  for (const f of ['sanitaer-text.js', 'sanitaer-palette.js', 'sanitaer-anlage.js', '../ansichten/sanitaer-liste.js', 'aktionen-sanitaer.js']) {
    const q = lies(f);
    assert.doesNotMatch(q, /Math\.(sqrt|pow|cbrt)|\b0\.(459|353|598|257)\b/, `${f}: Rechenkonstante oder Wurzel in der Oberfläche`);
    assert.doesNotMatch(q, /from '(\.\.\/)?\.?\/?api\.js'/, `${f}: ruft den Kern nicht selbst auf (Daten kommen von aussen)`);
  }
});

// ---------------------------------------------------------------------------------------------- Apparateart

const defArt = { name: 'sanitaerArt', typ: 'auswahl', auswahl: ['waschtisch', 'badewanne', 'wc_75'],
  auswahlNamen: { waschtisch: 'Waschtisch / Wandbecken', badewanne: 'Badewanne', wc_75: 'Klosettanlage bis 7.5 Liter Spülwassermenge' } };

test('Auswahl der Apparateart: Namen, erster Eintrag «Vorgabe der Klasse (…)» für nicht gesetzt', () => {
  const o = T.artOptionen(defArt, 'waschtisch');
  assert.deepEqual(o[0], ['', 'Vorgabe der Klasse (Waschtisch / Wandbecken)']);
  assert.deepEqual(o.slice(1), [['waschtisch', 'Waschtisch / Wandbecken'], ['badewanne', 'Badewanne'], ['wc_75', 'Klosettanlage bis 7.5 Liter Spülwassermenge']]);
  assert.deepEqual(T.artOptionen(defArt, null)[0], ['', 'Vorgabe der Klasse: keine – Art wählen']);   // Bodenablauf
  assert.equal(T.artName('xyz', {}), 'xyz');
  assert.equal(T.artName(null, {}), '');
});

test('Feld der Apparateart: Schlüssel als Wert, Name als Text, nicht gesetzt = leere Option, Unbekanntes bleibt sichtbar', () => {
  const h = P.artFeld(defArt, 'badewanne', 'waschtisch');
  assert.match(h, /data-ziel="eig:sanitaerArt"/);
  assert.match(h, /<option value="badewanne" selected>Badewanne<\/option>/);
  assert.match(h, /<option value="">Vorgabe der Klasse \(Waschtisch \/ Wandbecken\)<\/option>/);
  assert.match(h, /title="Badewanne"/);
  assert.match(P.artFeld(defArt, null, 'waschtisch'), /<option value="" selected>Vorgabe der Klasse/);
  assert.match(P.artFeld(defArt, 'alt_art', null), /<option value="alt_art" selected>alt_art \(gibt es nicht\)<\/option>/);
  assert.doesNotMatch(P.artFeld(defArt, null, null), /<option value="waschtisch" selected/);
});

test('Satz zur wirksamen Art und Werte unter der Art', () => {
  assert.equal(T.artHinweis({ woher: 'vorgabe', name: 'Waschtisch / Wandbecken' }), 'gilt: Waschtisch / Wandbecken (Vorgabe der Klasse)');
  assert.equal(T.artHinweis({ woher: 'eigenschaft', art: 'badewanne', name: 'Badewanne' }), 'gilt: Badewanne (am Apparat gesetzt)');
  assert.equal(T.artHinweis({ woher: 'familie', name: 'Dusche nicht staubar' }), 'gilt: Dusche nicht staubar (aus der Familie)');
  assert.match(T.artHinweis({ woher: 'keine' }), /keine Art – bitte wählen \(Befund P21\)/);
  assert.match(T.artHinweis({ woher: 'unbekannt', art: 'x' }), /«x» gibt es im Katalog nicht/);
  assert.equal(T.artHinweis(null), '');
  // WC: kein LU WW (nicht 0), Bodenablauf: nur DU
  assert.deepEqual(T.apparatWerte({ luKw: 1, du: 2 }), [
    { schluessel: 'luKw', label: 'LU KW', einheit: 'LU', text: '1' }, { schluessel: 'du', label: 'DU', einheit: 'DU', text: '2.0' }]);
  assert.deepEqual(T.apparatWerte({ du: 1.5 }).map((w) => w.label), ['DU']);
  assert.deepEqual(T.apparatWerte({}), []);
});

const H = { zeile: (label, feld, o = {}) => `<z l="${label}" w="${o.warum ?? ''}" k="${o.klasse ?? ''}">${feld}</z>`, nurLesen: (t) => `<t>${t}</t>` };

test('Zeilen unter der Apparateart: LU KW / LU WW / DU mit «?» (warum = Eigenschaft)', () => {
  const z = P.artZeilen({ def: defArt, wert: null, vorgabe: 'waschtisch', l: { sanitaer: { woher: 'vorgabe', name: 'Waschtisch / Wandbecken' }, abgeleitet: { luKw: 1, luWw: 1, du: 0.5 } } }, H);
  assert.equal(z.length, 5);   // Art, Satz, LU KW, LU WW, DU
  assert.match(z[0], /l="Apparateart" w="sanitaerArt"/);
  assert.match(z[1], /gilt: Waschtisch/);
  assert.match(z[2], /l="LU KW" w="luKw"[^>]*><t>1 LU<\/t>/);
  assert.match(z[3], /l="LU WW" w="luWw"/);
  assert.match(z[4], /l="DU" w="du"[^>]*><t>0\.5 DU<\/t>/);
  const ohne = P.artZeilen({ def: defArt, wert: null, vorgabe: null, l: { sanitaer: { woher: 'keine' }, abgeleitet: {} } }, H);
  assert.equal(ohne.length, 2);   // Art und Warnung, keine Werte
  assert.match(ohne[1], /ep-sanwarnung/);
});

test('Leitung: Summen mit «?», Apparate und Hinweise (Masche, ohne Wert)', () => {
  assert.deepEqual(T.leitungWerte({ summeLuKw: 24, summeLuWw: 0, groessterLu: 3 }).map((w) => [w.label, w.text, w.einheit]),
    [['ΣLU KW', '24', 'LU'], ['ΣLU WW', '0', 'LU'], ['Grösster Einzel-LU', '3', 'LU']]);
  assert.deepEqual(T.leitungWerte({ summeDu: 16.7 }).map((w) => [w.label, w.text]), [['ΣDU', '16.7']]);
  assert.deepEqual(T.leitungHinweise({ apparate: 15, richtung: 'Apparate stromab' }), ['15 Apparate (Apparate stromab)']);
  assert.deepEqual(T.leitungHinweise({ apparate: 1, richtung: 'Apparate stromauf', masche: true, ohneWert: ['WT-1.kw (ohne Apparateart)'] }),
    ['1 Apparat (Apparate stromauf)', 'Netz mit Masche – Summe nicht eindeutig (Befund P24)', 'ohne Wert: WT-1.kw (ohne Apparateart)']);
  const z = P.leitungZeilen({ abgeleitet: { summeLuKw: 24, summeLuWw: 6, groessterLu: 3 }, sanitaer: { apparate: 15, richtung: 'Apparate stromab', masche: false } }, H);
  assert.equal(z.length, 4);
  assert.match(z[0], /w="summeLuKw"/); assert.match(z[1], /w="summeLuWw"/); assert.match(z[2], /w="groessterLu"/);
  assert.deepEqual(P.leitungZeilen({ abgeleitet: { laenge_m: 1 } }, H), []);   // keine Sanitärleitung: nichts
  // die allgemeine Gruppe «Abgeleitet» zeigt diese Schlüssel nicht noch einmal
  assert.deepEqual(T.SANITAER_ABGELEITET.sort(), ['du', 'groessterLu', 'luKw', 'luWw', 'summeDu', 'summeLuKw', 'summeLuWw']);
});

test('Abflusskennzahl K: freie Zahl mit Hinweis, keine Auswahlliste', () => {
  assert.equal(T.K_HINWEIS, 'Wert aus SN 592000 nach Gebäudeart – Norm liegt nicht vor; Walkringen: 0.5');
  assert.match(P.kHinweisZeile(H), /Walkringen: 0\.5/);
  const d = klasse('Anlage').eigenschaften.find((e) => e.name === 'abflusskennzahl');
  assert.equal(d.typ, 'zahl');
  assert.equal(d.auswahl, undefined);
  assert.equal(klasse('Projektinformation').eigenschaften.find((e) => e.name === 'abflusskennzahl').typ, 'zahl');
});

// ---------------------------------------------------------------------------------------------- Spezialinstallationen

test('Tabelle: Wert des Kerns → Zeilen (nie «[object Object]»), Unbrauchbares wird übersprungen', () => {
  const z = T.spezialZeilen([{ bezeichnung: 'Garten', versorgung: 0.5, entsorgung: 0.25 }, { bezeichnung: 'Pool', versorgung: 1.5 }, 'x', null, [1]]);
  assert.deepEqual(z, [{ bezeichnung: 'Garten', versorgung: '0.5', entsorgung: '0.25' }, { bezeichnung: 'Pool', versorgung: '1.5', entsorgung: '' }]);
  assert.deepEqual(T.spezialZeilen(undefined), []);
  assert.deepEqual(T.spezialZeilen('Garten'), []);
  const h = P.spezialFeld([{ bezeichnung: 'Garten', versorgung: 0.5, entsorgung: 0.25 }]);
  assert.doesNotMatch(h, /\[object Object\]/);
  assert.match(h, /data-ziel="eig:spezialinstallationen" data-typ="spezial"/);
  assert.match(h, /value="Garten"/); assert.match(h, /class="sp-v" value="0\.5"/); assert.match(h, /class="sp-e" value="0\.25"/);
  assert.match(h, /Bezeichnung<\/th><th[^>]*>Versorgung l\/s<\/th><th[^>]*>Entsorgung l\/s<\/th>/);
  assert.match(h, /ep-spez-neu/); assert.match(h, /ep-spez-weg/);
  assert.equal((P.spezialFeld(null).match(/<tr>/g) ?? []).length, 1);   // nur die Kopfzeile
  assert.match(P.spezialZeileHtml({ bezeichnung: 'A"B' }), /value="A&quot;B"/);   // maskiert
});

test('Tabelle: Eingabe → Wert für element_aendern (Komma, leere Zeilen, unvollständige Zeilen, Löschen)', () => {
  assert.deepEqual(T.spezialWert([{ bezeichnung: 'Garten', versorgung: '0,5', entsorgung: '' }]), { wert: [{ bezeichnung: 'Garten', versorgung: 0.5 }], offen: 0 });
  assert.deepEqual(T.spezialWert([{ bezeichnung: ' Garten ', versorgung: '', entsorgung: '0.25' }]), { wert: [{ bezeichnung: 'Garten', entsorgung: 0.25 }], offen: 0 });
  assert.deepEqual(T.spezialWert([{ bezeichnung: 'Pool', versorgung: '1', entsorgung: '2' }, { bezeichnung: 'Garten', versorgung: '0,5', entsorgung: '' }]).wert,
    [{ bezeichnung: 'Pool', versorgung: 1, entsorgung: 2 }, { bezeichnung: 'Garten', versorgung: 0.5 }]);
  // frisch angefügte leere Zeile zählt nicht
  assert.deepEqual(T.spezialWert([{ bezeichnung: '', versorgung: '', entsorgung: '' }]), { wert: null, offen: 0 });
  assert.deepEqual(T.spezialWert([]), { wert: null, offen: 0 });
  assert.deepEqual(T.spezialWert(undefined), { wert: null, offen: 0 });
  // unvollständig: Bezeichnung ohne Zahl, Zahl ohne Bezeichnung – nichts senden, bis vollständig
  assert.equal(T.spezialWert([{ bezeichnung: 'Garten', versorgung: '', entsorgung: '' }]).offen, 1);
  assert.equal(T.spezialWert([{ bezeichnung: '', versorgung: '0,5', entsorgung: '' }]).offen, 1);
  const gemischt = T.spezialWert([{ bezeichnung: 'Pool', versorgung: '1', entsorgung: '' }, { bezeichnung: 'Garten', versorgung: '', entsorgung: '' }]);
  assert.equal(gemischt.offen, 1); assert.equal(gemischt.wert.length, 1);
  // ungültige Zahl → Fehler mit Text; negative Zahlen lehnt der Kern ab (nicht die Oberfläche)
  assert.throws(() => T.spezialWert([{ bezeichnung: 'Garten', versorgung: 'viel', entsorgung: '' }]), /Versorgung: bitte eine Zahl in l\/s/);
  assert.deepEqual(T.spezialWert([{ bezeichnung: 'Garten', versorgung: '-1', entsorgung: '' }]).wert, [{ bezeichnung: 'Garten', versorgung: -1 }]);
  assert.equal(T.spezialZahl(' ', 'x'), null);
  assert.equal(T.spezialZahl('1,25', 'x'), 1.25);
});

// ---------------------------------------------------------------------------------------------- aktive Anlage

const anlageA = { id: 'a1', kennzeichen: 'A-1', klasse: 'Anlage' }, anlageB = { id: 'a2', kennzeichen: 'A-2', klasse: 'Anlage' };
const sysKW = { id: 's1', kennzeichen: 'KW', klasse: 'System', teilVon: 'A-2' };
const strang = { id: 'st1', kennzeichen: 'KW-1', klasse: 'Strang', teilVon: 'KW' };
const rohr = { id: 'r1', kennzeichen: 'R-1', klasse: 'Rohr', system: 'KW', teilVon: 'KW-1' };
const wt = { id: 'w1', kennzeichen: 'WT-1', klasse: 'Waschtisch', system: 'KW' };
const lose = { id: 'x1', kennzeichen: 'X-1', klasse: 'WC' };
const alle = [anlageA, anlageB, sysKW, strang, rohr, wt, lose];
const el = (kz) => alle.find((e) => e.kennzeichen === kz || e.id === kz) ?? null;

test('Anlage eines Elements: aufwärts über system und teilVon', () => {
  assert.equal(A.anlageVon(anlageA, el), anlageA);
  assert.equal(A.anlageVon(sysKW, el), anlageB);
  assert.equal(A.anlageVon(strang, el), anlageB);
  assert.equal(A.anlageVon(rohr, el), anlageB);
  assert.equal(A.anlageVon(wt, el), anlageB);
  assert.equal(A.anlageVon(lose, el), null);
  assert.equal(A.anlageVon(null, el), null);
  const kreis = { id: 'k1', kennzeichen: 'K', klasse: 'System', teilVon: 'K' };
  assert.equal(A.anlageVon(kreis, (k) => (k === 'K' ? kreis : null)), null);   // endet trotz Kreis
});

test('aktive Anlage: Auswahl, offene Liste, einzige Anlage, sonst null (dann fragt der Dialog)', () => {
  assert.equal(A.aktiveAnlage({ auswahl: wt, aktiveAnsicht: null, anlagen: [anlageA, anlageB], element: el }), anlageB);
  const liste = { klasse: 'Ansicht', werte: { art: 'liste', liste: 'sanitaer_zusammenstellung', anlage: 'a-1' } };
  assert.equal(A.aktiveAnlage({ auswahl: null, aktiveAnsicht: liste, anlagen: [anlageA, anlageB], element: el }), anlageA);
  assert.equal(A.aktiveAnlage({ auswahl: null, aktiveAnsicht: { klasse: 'Ansicht', werte: { art: 'grundriss' } }, anlagen: [anlageA], element: el }), anlageA);
  assert.equal(A.aktiveAnlage({ auswahl: lose, aktiveAnsicht: null, anlagen: [anlageA, anlageB], element: el }), null);
  assert.equal(A.aktiveAnlage({ auswahl: null, aktiveAnsicht: null, anlagen: [], element: el }), null);
});

test('Liste der Anlage finden; Kennzeichen- und Namensvorschlag wie im Dialog', () => {
  const l1 = { kennzeichen: 'AN-LI-SANITAER-A-1', werte: { art: 'liste', liste: 'sanitaer_zusammenstellung', anlage: 'A-1' } };
  const l2 = { kennzeichen: 'AN-LI-RAUM', werte: { art: 'liste', liste: 'raumliste' } };
  const vl = { kennzeichen: 'VL', werte: { art: 'liste', liste: 'sanitaer_zusammenstellung', anlage: 'A-2', vorlage: true } };
  assert.equal(A.listeFuerAnlage([l2, l1, vl], 'a-1'), l1);
  assert.equal(A.listeFuerAnlage([l2, l1, vl], 'A-2'), null);   // Vorlagen zählen nicht
  assert.equal(A.listenKennzeichen(anlageA), 'AN-LI-SANITAER-A-1');
  assert.equal(A.listenName(anlageA), 'Sanitär-Zusammenstellung (W3) – A-1');
});

// ---------------------------------------------------------------------------------------------- Liste «Sanitär-Zusammenstellung (W3)»

/** Block kurvenwahl der Antwort wie SanitaerAbfragen.KurvenwahlJson: Regel und Kurven mit «grösster LU» aus dem Katalog (rechnung.spitzeKurve3/5). */
function kurvenwahlAusKatalog(kat) {
  const r = kat.rechnung;
  return {
    regel: r.kurvenwahl.regel, quelle: r.kurvenwahl.quelle, bestaetigt: r.kurvenwahl.bestaetigt,
    kurven: ['spitzeKurve3', 'spitzeKurve5'].map((s) => ({ schluessel: s, grosserLu: r[s].grosserLu, bereichLs: r[s].bereichLs, bereichZelle: r[s].bereichZelle, zelle: r[s].zelle })),
  };
}

/** Antwort der Abfrage sanitaer_zusammenstellung im Referenzfall Walkringen (Katalog referenz; Aufbau wie SanitaerAbfragen.cs). Geschosse EG und OG fiktiv. */
function walkringen(k = 0.5) {
  const r = SKAT.referenz;
  const arten = Object.entries(r.anzahl).map(([schl, n]) => ({ a: SKAT.arten.find((x) => x.key === schl), n }));
  const zeilen = arten.map(({ a, n }) => ({
    art: a.key, name: a.name, zeile: a.zeile, luKw: a.luKw, luWw: a.luWw, du: a.du, jeGeschoss: [Math.ceil(n / 2), Math.floor(n / 2)], anzahl: n,
    summeLuKw: a.luKw === null ? null : a.luKw * n, summeLuWw: a.luWw === null ? null : a.luWw * n, summeDu: a.du === null ? null : a.du * n,
    apparate: [`${a.key}-1`], quelle: `Katalog sanitaer_belastungswerte.json: LU_DU Zusammenstellung_.xlsx, Blatt MFHA, Zeile ${a.zeile} (Walkringen, Stand 14.03.2024)`,
  }));
  return {
    anlage: 'A-1', name: 'Testanlage', quelle: { dokument: SKAT.quelle.dokument, objekt: SKAT.quelle.objekt, stand: SKAT.quelle.stand },
    geschosse: [{ kennzeichen: 'EG', name: 'Erdgeschoss', kote: 0 }, { kennzeichen: 'OG', name: 'Obergeschoss', kote: 3000 }],
    zeilen, apparate: 49, summeLuKw: r.summeLuKw, summeLuWw: r.summeLuWw, summeDu: r.summeDu, totalLu: r.totalLu, volumenstrom: r.volumenstrom,
    spitzeKurve3: r.spitzeKurve3, spitzeKurve5: r.spitzeKurve5, groessterLu: 5, massgebendeKurve: SKAT.rechnung.spitzeKurve5.grosserLu, spitzendurchfluss: r.spitzeKurve5,
    kurvenwahl: kurvenwahlAusKatalog(SKAT),
    spezialinstallationen: [], spezialVersorgung: 0, spezialEntsorgung: 0, spitzeGesamtKurve3: r.spitzeKurve3, spitzeGesamtKurve5: r.spitzeKurve5, spitzeGesamt: r.spitzeKurve5,
    abflusskennzahl: k, abflusskennzahlHerkunft: k === null ? null : 'Anlage A-1: Test', schmutzwasser: k === null ? null : r.schmutzwasser, gesamtabfluss: k === null ? null : r.schmutzwasser,
    formeln: { totalLu: 'ΣLU KW + ΣLU WW (B55 = M43+N43)', volumenstrom: 'Total LU × 0.1 l/s (B56 = B55*0.1)', spitzeKurve3: 'C57 = B56^0.353*0.459 – Spitzenvolumenstrom bei 0.3 bis 300 l/s, grösster LU 3',
      spitzeKurve5: 'I57 = B56^0.257*0.598 – Spitzenvolumenstrom bei 0.5 bis 15 l/s, grösster LU 5', massgebendeKurve: 'grösster Einzel-LU > 3 → Kurve 5',
      spitzeGesamt: 'plus Spezialinstallationen Versorgung (C59 = C57+I51 bzw. I59 = I57+I51)', schmutzwasser: 'Q_WW = K × √ΣDU (M66 = M65*((M64)^(1/2)))', gesamtabfluss: 'Q_tot = Q_WW + Spezialinstallationen Entsorgung (M67 = M66+N51)' },
    ohneArt: [], hinweise: k === null ? ['Abflusskennzahl K fehlt (Anlage und Projektinformation): kein Q_WW und kein Q_tot (Befund P25).'] : [], hinweis: 'Summen der Belastungswerte, keine Dimensionierung.',
  };
}

const zeileMit = (t, beginn) => t.zeilen.find((z) => String(z.groesse ?? z.art).startsWith(beginn));
const text = (sp, z) => (typeof sp.format === 'function' ? sp.format(z[sp.schluessel]) : formatWert(sp.schluessel, z[sp.schluessel]));
const zelle = (t, z, schl) => text(t.spalten.find((s) => s.schluessel === schl), z);

test('Aufbau wie das Excel: Spalten, Zeilen in Katalogreihenfolge, Totalzeile (Referenzfall Walkringen)', () => {
  const d = aufbereiten('sanitaer_zusammenstellung', walkringen());
  assert.equal(d.titel, 'Sanitär-Zusammenstellung (W3)');
  assert.equal(d.projekt, 'A-1 Testanlage');
  assert.equal(d.tabellen.length, 3);
  const [ap, tw, ew] = d.tabellen;
  assert.deepEqual(ap.spalten.map((s) => s.titel), ['Apparateart', 'LU KW', 'LU WW', 'DU', 'EG', 'OG', 'Anzahl', 'ΣLU KW', 'ΣLU WW', 'ΣDU']);
  assert.equal(ap.zeilen.length, 7);
  assert.ok(ap.zeilen.some((z) => z.art === 'Waschtisch / Wandbecken') && ap.zeilen.some((z) => z.art === 'Badewanne'));
  const wc = ap.zeilen.find((z) => z.art.startsWith('Klosettanlage bis 7.5'));
  assert.deepEqual(ap.spalten.map((s) => text(s, wc)), [wc.art, '1', '', '2,0', '5', '5', '10', '10', '', '20,0']);   // kein LU WW: leere Zelle, nicht «0»
  const t = ap.fuss[0];
  assert.deepEqual(ap.spalten.map((s) => text(s, t)), ['Total', '', '', '', '', '', '49', '80', '52', '38,8']);   // ΣKW 80, ΣWW 52, ΣDU 38.8 (Excel)
  assert.equal(ap.zeilen.every((z) => /Zeile \d+/.test(z.titel.art) && /LU_DU Zusammenstellung_\.xlsx/.test(z.titel.art)), true, 'Tooltip der Art nennt Excel und Zeile');
  // Katalogreihenfolge = aufsteigende Excel-Zeile
  const excelZeilen = ap.zeilen.map((z) => Number(/Zeile (\d+)/.exec(z.titel.art)[1]));
  assert.deepEqual([...excelZeilen].sort((a, b) => a - b), excelZeilen);
  assert.ok(tw && ew);
});

test('Blöcke Trinkwasser und Entwässerung: gerundete Zahlen, massgebende Kurve fett, die andere klein, Tooltips mit Excel-Zelle', () => {
  const [, tw, ew] = aufbereiten('sanitaer_zusammenstellung', walkringen()).tabellen;
  const w = (t, n) => zelle(t, zeileMit(t, n), 'wert');
  assert.equal(w(tw, 'Total LU'), '132');
  assert.equal(w(tw, 'Volumenstrom'), '13,20');
  assert.match(zeileMit(tw, 'Spitzendurchfluss Kurve «grösster LU 5»').groesse, /– massgebend$/);
  assert.equal(zeileMit(tw, 'Spitzendurchfluss Kurve «grösster LU 5»').klasse, 'li-massgebend');
  assert.equal(w(tw, 'Spitzendurchfluss Kurve «grösster LU 5»'), '1,16');
  assert.match(zeileMit(tw, 'Spitzendurchfluss Kurve «grösster LU 3»').groesse, /\(nicht massgebend\)$/);
  assert.equal(zeileMit(tw, 'Spitzendurchfluss Kurve «grösster LU 3»').klasse, 'li-nebenwert');
  assert.equal(w(tw, 'Spitzendurchfluss Kurve «grösster LU 3»'), '1,14');
  assert.equal(w(tw, 'Spitzendurchfluss gesamt (Kurve «grösster LU 5»'), '1,16');
  assert.equal(w(tw, 'Spezialinstallationen Versorgung gesamt'), '0');
  assert.equal(w(ew, 'ΣDU'), '38,8');
  assert.equal(w(ew, 'Abflusskennzahl K'), '0,5');
  assert.equal(w(ew, 'Schmutzwasserabfluss Q_WW'), '3,11');
  assert.equal(w(ew, 'Gesamtabfluss Q_tot'), '3,11');
  // Tooltips: Formel mit Excel-Zelle und der genaue (ungerundete) Wert
  assert.match(zeileMit(tw, 'Total LU').titel['*'], /B55 = M43\+N43/);
  assert.match(zeileMit(tw, 'Volumenstrom').titel['*'], /B56 = B55\*0\.1/);
  assert.match(zeileMit(tw, 'Spitzendurchfluss Kurve «grösster LU 3»').titel['*'], /C57 = B56\^0\.353\*0\.459/);
  assert.match(zeileMit(tw, 'Spitzendurchfluss Kurve «grösster LU 3»').titel['*'], /genau: 1,1412355278976094 l\/s/);
  assert.match(zeileMit(tw, 'Spitzendurchfluss Kurve «grösster LU 5»').titel['*'], /I57 = B56\^0\.257\*0\.598/);
  assert.match(zeileMit(ew, 'Schmutzwasserabfluss Q_WW').titel['*'], /M66 = M65\*\(\(M64\)\^\(1\/2\)\)/);
  assert.match(zeileMit(ew, 'Gesamtabfluss Q_tot').titel['*'], /M67 = M66\+N51/);
  // die Formeln stehen zusätzlich in einer Spalte, die nur die CSV-Datei hat
  assert.equal(tw.spalten.find((s) => s.schluessel === 'formel').nurCsv, true);
  assert.equal(tw.ohneSortieren, true);
});

test('Spezialinstallationen: eine Zeile je Eintrag, Summe, Spitze gesamt und Q_tot kommen vom Kern', () => {
  const d = walkringen();
  const k5 = d.spitzeKurve5;
  Object.assign(d, { spezialinstallationen: [{ bezeichnung: 'Garten', versorgung: 0.5, entsorgung: 0.25 }, { bezeichnung: 'Pool', versorgung: 1, entsorgung: 0 }],
    spezialVersorgung: 1.5, spezialEntsorgung: 0.25, spitzeGesamtKurve5: k5 + 1.5, spitzeGesamt: k5 + 1.5, gesamtabfluss: d.schmutzwasser + 0.25 });
  const [, tw, ew] = aufbereiten('sanitaer_zusammenstellung', d).tabellen;
  const w = (t, n) => zelle(t, zeileMit(t, n), 'wert');
  assert.equal(w(tw, 'Spezialinstallation: Garten'), '0,5');
  assert.equal(w(tw, 'Spezialinstallation: Pool'), '1');
  assert.equal(w(tw, 'Spezialinstallationen Versorgung gesamt'), '1,5');
  assert.equal(w(tw, 'Spitzendurchfluss gesamt (Kurve «grösster LU 5»'), '2,66');   // 1.1606… + 1.5
  assert.equal(w(ew, 'Spezialinstallation: Garten'), '0,25');
  assert.equal(zeileMit(ew, 'Spezialinstallation: Pool'), undefined, 'ohne Entsorgung (0) steht der Eintrag nur im Block Trinkwasser');
  assert.equal(w(ew, 'Spezialinstallationen Entsorgung gesamt'), '0,25');
  assert.equal(w(ew, 'Gesamtabfluss Q_tot'), '3,36');   // 3.1145 + 0.25
});

test('ohne Abflusskennzahl K: «K fehlt» statt K, Q_WW und Q_tot – keine Zahl', () => {
  const d = aufbereiten('sanitaer_zusammenstellung', walkringen(null));
  const ew = d.tabellen[2];
  for (const n of ['Abflusskennzahl K', 'Schmutzwasserabfluss Q_WW', 'Gesamtabfluss Q_tot']) {
    const z = zeileMit(ew, n);
    assert.equal(zelle(ew, z, 'wert'), 'K fehlt', n);
    assert.equal(z.zellklassen.wert, 'li-fehlt', n);
    assert.equal(z.einheit, '', n);
  }
  assert.equal(zelle(ew, zeileMit(ew, 'ΣDU'), 'wert'), '38,8');   // ΣDU bleibt
  // der Hinweis des Kerns steht unter den Blöcken (Tabelle «Hinweise»), der Kopf nennt nur die Zahl
  assert.equal(d.tabellen.length, 4);
  assert.equal(d.tabellen[3].titel, 'Hinweise');
  assert.ok(d.tabellen[3].zeilen.some((z) => /Abflusskennzahl K fehlt/.test(z.hinweis)), 'Hinweis des Kerns unter den Blöcken');
  assert.ok(d.zusammenfassung.includes('1 Hinweis des Kerns unter den Tabellen'));
  assert.ok(!d.zusammenfassung.some((t) => /Abflusskennzahl K fehlt/.test(t)), 'nicht doppelt im Kopf');
  assert.doesNotMatch(JSON.stringify(ew.zeilen.map((z) => z.wert)), /NaN|null|undefined/);
});

test('Hinweise des Kerns (Prüfbericht 4A): eigene Tabelle unter den Blöcken, Texte wörtlich, Tooltip, in der CSV; ohne Hinweise keine Tabelle', () => {
  const kurven = 'Kurvenwahl nicht im Excel, aus HKAP, nicht bestätigt: massgebend ist die Kurve «grösster LU 5» nach der Regel «grösster Einzel-LU > 3 → Kurve «grösster LU 5», sonst Kurve «grösster LU 3»» (Katalog rechnung.kurvenwahl); beide Kurven sind ausgewiesen.';
  const ohneWeg = 'ΣLU KW enthält 1 Apparat ohne Weg im KW-Netz: WC-1 – in den Netzsummen fehlen sie stromauf (Befund P26).';
  const ww = 'ΣLU WW enthält 2 Apparate ohne WW-Verbindung: WT-1, WT-2 – die Zusammenstellung zählt Stückzahl × Wert wie das Excel, die Netzsummen (sanitaer_belastung) zählen sie nicht.';
  const bereich = 'Volumenstrom 0.2 l/s liegt ausserhalb des Bereichs der Kurve «grösster LU 5»: 0.5 bis 15 l/s laut Katalog (I55 «…», I56 «…») – Spitze dieser Kurve prüfen.';
  const d = aufbereiten('sanitaer_zusammenstellung', { ...walkringen(), hinweise: [ww, ohneWeg, '', null, bereich, kurven] });
  assert.equal(d.tabellen.length, 4);
  const h = d.tabellen[3];
  assert.deepEqual([h.titel, h.ohneSortieren, h.schluessel, h.klasse], ['Hinweise', true, null, 'li-san li-hinweise']);
  assert.deepEqual(h.spalten.map((s) => s.titel), ['Hinweis']);
  assert.deepEqual(h.zeilen.map((z) => z.hinweis), [ww, ohneWeg, bereich, kurven]);   // leere Einträge fallen weg, Reihenfolge des Kerns
  assert.equal(h.zeilen[1].titel['*'], ohneWeg);
  assert.ok(d.zusammenfassung.includes('4 Hinweise des Kerns unter den Tabellen'));
  const csv = CSV.csvAus(d).slice(1).split('\r\n');
  assert.ok(csv.includes('Hinweise') && csv.includes('Hinweis'));
  assert.ok(csv.some((z) => z.includes('ohne Weg im KW-Netz: WC-1') && z.includes('Befund P26')));
  assert.equal(aufbereiten('sanitaer_zusammenstellung', walkringen()).tabellen.length, 3);   // keine Hinweise: keine vierte Tabelle
  assert.ok(!aufbereiten('sanitaer_zusammenstellung', walkringen()).zusammenfassung.some((t) => /Hinweis/.test(t) && /unter den Tabellen/.test(t)));
});

test('Kopf der Liste: Anlage, Stückzahl, Quelle (Excel) und der Hinweis «keine Dimensionierung»', () => {
  const d = aufbereiten('sanitaer_zusammenstellung', walkringen());
  assert.equal(d.zusammenfassung[0], 'Anlage A-1 Testanlage · 49 Apparate');
  assert.match(d.zusammenfassung[1], /^Quelle: LU_DU Zusammenstellung_\.xlsx \(Walkringen .*Stand 14\.03\.2024\)$/);
  assert.ok(d.zusammenfassung.some((t) => /keine Dimensionierung/.test(t)));
});

test('leere Anlage und fehlende Felder: keine Ausnahme, keine «undefined»-Texte', () => {
  const d = aufbereiten('sanitaer_zusammenstellung', { anlage: 'A-9', apparate: 0, geschosse: [], zeilen: [], summeLuKw: 0, summeLuWw: 0, summeDu: 0, totalLu: 0, volumenstrom: 0, abflusskennzahl: null });
  assert.equal(d.tabellen[0].zeilen.length, 0);
  assert.deepEqual(d.tabellen[0].spalten.map((s) => s.titel), ['Apparateart', 'LU KW', 'LU WW', 'DU', 'Anzahl', 'ΣLU KW', 'ΣLU WW', 'ΣDU']);
  const csv = CSV.csvAus(d);
  assert.doesNotMatch(csv, /undefined|NaN|\[object Object\]/);
  assert.doesNotThrow(() => aufbereiten('sanitaer_zusammenstellung', null));
});

test('CSV: beide Blöcke, Formeln nur in der Datei, Totalzeile, Dezimalkomma wie in der Ansicht', () => {
  const d = aufbereiten('sanitaer_zusammenstellung', walkringen());
  const csv = CSV.csvAus(d);
  const z = csv.slice(1).split('\r\n');
  assert.equal(csv[0], '﻿');
  assert.equal(z[0], 'Apparate (LU und DU je Stück, Stück je Geschoss, Summen)');
  assert.equal(z[1], 'Apparateart;LU KW;LU WW;DU;EG;OG;Anzahl;ΣLU KW;ΣLU WW;ΣDU');
  assert.ok(z.includes('Total;;;;;;49;80;52;38,8'));
  assert.ok(z.includes('Trinkwasser') && z.includes('Entwässerung'));
  assert.equal(z.filter((x) => x === 'Grösse;Wert;Einheit;Formel (Excel-Zelle)').length, 2);
  assert.ok(z.includes('Total LU;132;LU;ΣLU KW + ΣLU WW (B55 = M43+N43)'));
  assert.ok(z.includes('Volumenstrom;13,20;l/s;Total LU × 0.1 l/s (B56 = B55*0.1)'));
  assert.ok(z.some((x) => x.startsWith('Schmutzwasserabfluss Q_WW;3,11;l/s;Q_WW = K × √ΣDU (M66 = M65*((M64)^(1/2)))')));
  assert.ok(z.some((x) => x.startsWith('Abflusskennzahl K;0,5;;Anlage A-1: Test')));
  assert.equal(csv.endsWith('\r\n'), true);
  // ohne K: «K fehlt» auch in der Datei
  const ohne = CSV.csvAus(aufbereiten('sanitaer_zusammenstellung', walkringen(null))).split('\r\n');
  assert.ok(ohne.some((x) => x.startsWith('Gesamtabfluss Q_tot;K fehlt;;')));
  assert.equal(CSV.csvZeilenzahl(d), d.tabellen.reduce((n, t) => n + t.zeilen.length, 0));   // Kopf- und Totalzeilen zählen nicht
});

test('Kurvennamen «grösster LU n» aus dem Katalog (kurvenwahl.kurven), nicht fest im Code – Ansicht und CSV (Prüfbericht 4A)', () => {
  // Katalog: die Liste nennt genau die Zahlen aus rechnung.spitzeKurve3/5.grosserLu
  const n3 = SKAT.rechnung.spitzeKurve3.grosserLu, n5 = SKAT.rechnung.spitzeKurve5.grosserLu;
  assert.deepEqual(L.kurvenNummern(walkringen()), { kurve3: n3, kurve5: n5 });
  const [, tw] = aufbereiten('sanitaer_zusammenstellung', walkringen()).tabellen;
  assert.ok(tw.zeilen.some((z) => z.groesse === `Spitzendurchfluss Kurve «grösster LU ${n3}» (nicht massgebend)`));
  assert.ok(tw.zeilen.some((z) => z.groesse === `Spitzendurchfluss Kurve «grösster LU ${n5}» – massgebend`));

  // abgewandelter Katalog (FIKTIV, nur für den Test): grosserLu 4 und 6, der Kern wählt 6 – die Liste folgt ohne Codeänderung
  const kat = structuredClone(SKAT);
  kat.rechnung.spitzeKurve3.grosserLu = 4; kat.rechnung.spitzeKurve5.grosserLu = 6;
  const d = { ...walkringen(), kurvenwahl: kurvenwahlAusKatalog(kat), massgebendeKurve: 6 };
  const auf = aufbereiten('sanitaer_zusammenstellung', d);
  const t = auf.tabellen[1];
  const namen = t.zeilen.map((z) => z.groesse);
  assert.ok(namen.includes('Spitzendurchfluss Kurve «grösster LU 4» (nicht massgebend)'), JSON.stringify(namen));
  assert.ok(namen.includes('Spitzendurchfluss Kurve «grösster LU 6» – massgebend'), JSON.stringify(namen));
  assert.equal(zeileMit(t, 'Spitzendurchfluss Kurve «grösster LU 6»').klasse, 'li-massgebend');
  assert.equal(zeileMit(t, 'Spitzendurchfluss Kurve «grösster LU 4»').klasse, 'li-nebenwert');
  assert.equal(zelle(t, zeileMit(t, 'Spitzendurchfluss Kurve «grösster LU 6»'), 'wert'), '1,16');   // Wert spitzeKurve5 des Kerns
  assert.ok(namen.includes('Spitzendurchfluss gesamt (Kurve «grösster LU 6» + Spezialinstallationen)'), JSON.stringify(namen));
  assert.ok(namen.includes('Spitzendurchfluss gesamt Kurve «grösster LU 4» (nicht massgebend)'), JSON.stringify(namen));
  assert.ok(!namen.some((n) => /grösster LU [35]»/.test(n)), 'keine fest eingetragenen Nummern 3 bzw. 5');
  const csv = CSV.csvAus(auf).slice(1).split('\r\n');
  assert.ok(csv.some((z) => z.startsWith('Spitzendurchfluss Kurve «grösster LU 6» – massgebend;1,16;l/s;I57 = B56^0.257*0.598')), 'CSV: Kurve 6 massgebend');
  assert.ok(csv.some((z) => z.startsWith('Spitzendurchfluss Kurve «grösster LU 4» (nicht massgebend);1,14;l/s;C57 = B56^0.353*0.459')), 'CSV: Kurve 4');
  assert.ok(csv.some((z) => z.startsWith('Spitzendurchfluss gesamt (Kurve «grösster LU 6» + Spezialinstallationen);1,16;l/s;')));
  assert.ok(!csv.some((z) => /grösster LU [35]»/.test(z.split(';')[0])), 'CSV ohne feste Nummern');

  // der Kern wählt die kleinere Kurve (4): sie ist massgebend, die andere Gesamtspitze trägt die 6
  const t4 = aufbereiten('sanitaer_zusammenstellung', { ...d, massgebendeKurve: 4, spitzeGesamt: d.spitzeGesamtKurve3 }).tabellen[1];
  assert.match(zeileMit(t4, 'Spitzendurchfluss Kurve «grösster LU 4»').groesse, /– massgebend$/);
  assert.match(zeileMit(t4, 'Spitzendurchfluss Kurve «grösster LU 6»').groesse, /\(nicht massgebend\)$/);
  assert.ok(t4.zeilen.some((z) => z.groesse === 'Spitzendurchfluss gesamt Kurve «grösster LU 6» (nicht massgebend)'));

  // ohne Angabe der Kurven: «?» statt einer Zahl, kein «massgebend»/«nicht massgebend» geraten, keine Ausnahme
  const ohne = { ...walkringen() };
  delete ohne.kurvenwahl;
  assert.deepEqual(L.kurvenNummern(ohne), { kurve3: null, kurve5: null });
  const to = aufbereiten('sanitaer_zusammenstellung', ohne).tabellen[1];
  assert.equal(to.zeilen.filter((z) => z.groesse === 'Spitzendurchfluss Kurve «grösster LU ?»').length, 2);
  assert.ok(!to.zeilen.some((z) => /nicht massgebend/.test(z.groesse)));
  assert.doesNotMatch(CSV.csvAus(aufbereiten('sanitaer_zusammenstellung', ohne)), /undefined|NaN/);
  assert.deepEqual(L.kurvenNummern(null), { kurve3: null, kurve5: null });
});

test('andere Listen unverändert: formatWert und CSV ohne Spaltenformat wie vorher', () => {
  const d = aufbereiten('raumliste', { ableitung: 'Raumliste', zeilen: [{ raum: 'R-1', flaeche_m2: 12.345, hoehe_m: 2.5 }], anzahl: 1 });
  assert.match(CSV.csvAus(d), /R-1;12,35;2,50/);
  assert.equal(formatWert('flaeche_m2', 1.5), '1,50');
});

// ---------------------------------------------------------------------------------------------- Verdrahtung

test('Verdrahtung: Aktion im Register Gebäudetechnik › Apparate und Armaturen, Liste im Dialog, Symbole in beiden Sätzen, Stylesheet', () => {
  const akt = lies('./aktionen-sanitaer.js');
  assert.match(akt, /id: 'sanitaer\.zusammenstellung'[\s\S]*symbol: 'sanZusammenstellung'[\s\S]*register: 'gebaeudetechnik', gruppe: 'Apparate und Armaturen'/);
  assert.match(lies('./aktionen.js'), /import '\.\/aktionen-sanitaer\.js'/);
  assert.match(akt, /element_anlegen/); assert.match(akt, /liste: 'sanitaer_zusammenstellung'/);
  const ans = lies('./aktionen-ansicht.js');
  assert.match(ans, /sanitaer_zusammenstellung: \['Sanitär-Zusammenstellung \(W3\)', 'SANITAER'\]/);
  assert.match(ans, /eig\.anlage = q\('\.f-anlage'\)\.value/);
  // Katalog: Auswahl der Listen und Ansichtseigenschaften
  const ansicht = klasse('Ansicht').eigenschaften;
  assert.ok(ansicht.find((e) => e.name === 'liste').auswahl.includes('sanitaer_zusammenstellung'));
  assert.equal(ansicht.find((e) => e.name === 'belastung').typ, 'bool');
  assert.equal(ansicht.find((e) => e.name === 'anlage').typ, 'text');
  // Symbole: Strich und Revit-Stil (32 und 16, eigene Zeichnung), verschieden von den Nachbarn
  const ersatz = S.symbol('gibtsnicht');
  assert.notEqual(S.symbol('sanZusammenstellung'), ersatz);
  assert.notEqual(S.symbol('sanZusammenstellung'), S.symbol('liste'));
  assert.ok(RV.hatRevitFassung('sanZusammenstellung'));
  assert.match(RV.revitSymbol('sanZusammenstellung', 32), /viewBox="0 0 32 32"/);
  assert.match(RV.revitSymbol('sanZusammenstellung', 16), /viewBox="0 0 16 16"/);
  assert.notEqual(RV.revitInhalt('sanZusammenstellung', false), RV.revitInhalt('sanZusammenstellung', true));
  assert.ok(RV.ZUORDNUNG.some((z) => z[0] === 'sanZusammenstellung' && z[2] === 'eigenes'));
  assert.match(lies('../../index.html'), /css\/sanitaer\.css/);
  // Palette: Bausteine eingebunden, Tabelle statt Kommaliste, Schalter belastung mit Vorgabe ein
  const pal = lies('./eigenschaften.js');
  assert.match(pal, /from '\.\/sanitaer-palette\.js'/);
  assert.match(pal, /d\.name === 'spezialinstallationen' && d\.typ === 'liste'/);
  assert.match(pal, /d\.name === 'belastung' \? wert !== false : wert === true/);
  assert.match(pal, /abfrage\('sanitaer_katalog'/);
  // Liste: Sanitär-Zusammenstellung wird mit der Anlage der Ansicht abgefragt
  assert.match(lies('../ansichten/liste.js'), /NAMEN_MODUL4\.includes\(n\)\) return ansicht\?\.werte\?\.anlage \? \{ anlage: ansicht\.werte\.anlage \}/);
});

test('Verdrahtung: keine neuen Register, die Gruppe «Apparate und Armaturen» gibt es schon', () => {
  assert.match(lies('./menueband.js'), /const REGISTER = \[\['datei', 'Datei'\], \['architektur', 'Architektur'\], \['huelle', 'Hülle'\], \['gebaeudetechnik', 'Gebäudetechnik'\], \['einfuegen', 'Einfügen'\], \['beschriften', 'Beschriften'\], \['aendern', 'Ändern'\], \['koordination', 'Koordination'\], \['ansicht', 'Ansicht'\], \['verwalten', 'Verwalten'\]\]/);
  assert.match(lies('../familien/aktionen-familien.js'), /register: 'gebaeudetechnik', gruppe: 'Apparate und Armaturen'/);
});

test('Vorgabe «Vorgabe der Klasse»: die Oberfläche kennt keine Zuordnung Klasse → Art (sie kommt aus sanitaer_katalog)', () => {
  const q = lies('./sanitaer-palette.js') + lies('./sanitaer-text.js') + lies('./eigenschaften.js');
  assert.doesNotMatch(q, /waschmaschine_6|wc_75|'Waschtisch': ?'waschtisch'/, 'keine Katalogschlüssel in der Oberfläche');
  assert.match(lies('./eigenschaften.js'), /san\.vorgabeJeKlasse\?\.\[el\.klasse\]/);
});
