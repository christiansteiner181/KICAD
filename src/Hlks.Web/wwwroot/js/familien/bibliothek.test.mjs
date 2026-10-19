// Tests der reinen Logik der Familienbibliothek in der Oberfläche (Modul 2, Auftrag C3) ohne Browser: Marken und Knoten im Projektbrowser, Auswahl «Familien im
// Explorer …» (mit der Spiegelung der Kernregel Sichtbar), Aktualisieren (Wege, Versionswahl, Vorschau), Familie laden, Statusleiste und die Verdrahtung
// (Menüband, Symbole, CSS, Befehle des Kerns). Aufruf:  node --test src/Hlks.Web/wwwroot/js/familien/bibliothek.test.mjs
// Alle Daten sind fiktiv (Aufbau der Antworten wie in FamilienBibliothek.cs und FamilienBibliothekVorschau.cs). Die Gegenprobe der Spiegelung gegen den echten Kern
// (Feld «sichtbar») macht der Browser-Test bibliothek.browser.mjs.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const T = await import('./bibliothek-text.js');
const B = await import('./browser-knoten.js');
const sym = await import('../oberflaeche/symbole.js');
const lies = (rel) => readFileSync(path.resolve(hier, rel), 'utf8');

// ------------------------------------------------------------------------------------------------ Testdaten (fiktiv)

const stand = (art, extra = {}) => ({ art, text: `Text ${art}`, version: null, aktuelleVersion: 3, abzweig: null, abzweigeOffen: [], warteschlange: false, offline: false, ...extra });
const projektFam = (kz, name, kategorie, status, bibliothekStand, extra = {}) => ({ kennzeichen: kz, name, id: `id-${kz}`, kategorie, status, version: 2, typen: ['T1', 'T2'], verwendungen: 0, gueltig: true, definitionsfehler: 0, bibliothekStand, ...extra });
const bibFam = (id, name, kategorie, gewerk, hersteller, status, extra = {}) => ({ quelle: 'bibliothek', id, name, kategorie, gewerk, hersteller, produkt: `P-${id}`, status, version: 3, versionen: 3, imProjekt: null, sichtbar: false, abzweigeOffen: [], ...extra });
const BIB = [
  bibFam('a1', 'Heizkörper Typ 22', 'Heizkoerper', 'heizung', 'Fiktiv AG', 'freigegeben'),
  bibFam('a2', 'Heizkörper Typ 33', 'Heizkoerper', 'heizung', 'fiktiv ag', 'entwurf'),
  bibFam('a3', 'Speicher 500', 'Speicher', 'heizung', 'Muster GmbH', 'entwurf'),
  bibFam('a4', 'Waschtisch', 'Waschtisch', 'sanitaer', null, 'freigegeben'),
  bibFam('a5', 'Zuluftdurchlass', 'Zuluftdurchlass', 'lueftung', 'Muster GmbH', 'freigegeben', { imProjekt: 'FAM-9' }),
  { quelle: 'mitgeliefert', id: 'm1', datei: 'test_rohr.familie.json', name: 'Testrohr', kategorie: 'Rohr', gewerk: 'sanitaer', hersteller: null, status: 'freigegeben', version: 1, imProjekt: null, sichtbar: true },
];

// ================================================================================================ Stand und Marken

test('Stand: Marken nach bibliothekStand – nur neuere Version, parallel geändert und noch nicht gespeichert tragen eine', () => {
  assert.equal(T.standMarke(stand('neuere_version')), 'neuere Version');
  assert.equal(T.standMarke(stand('parallel_geaendert')), 'parallel geändert');
  assert.equal(T.standMarke(stand('im_projekt_geaendert', { warteschlange: true })), 'noch nicht gespeichert');
  for (const art of ['aktuell', 'nicht_im_projekt', 'nicht_in_bibliothek', 'gesperrt']) assert.equal(T.standMarke(stand(art)), '', art);
  assert.equal(T.standMarke(null), '');
  assert.equal(T.aktualisierbar(stand('neuere_version')), true);
  assert.equal(T.aktualisierbar(stand('parallel_geaendert')), true);
  assert.equal(T.aktualisierbar(stand('im_projekt_geaendert')), false, 'noch nicht gespeichert: kein Knopf im Browser (geht über das Kontextmenü)');
  assert.equal(T.aktualisierbar(stand('aktuell')), false);
  assert.match(T.standTipp(stand('im_projekt_geaendert', { warteschlange: true, offline: true })), /Warteschlange.*nicht erreichbar/);
  assert.equal(T.standTipp(null), '');
  const f = projektFam('FAM-1', 'X', 'Ventil', 'freigegeben', stand('neuere_version'));
  const k = T.standKnoepfe(f);
  assert.equal(k.length, 1);
  assert.deepEqual([k[0].aktion, k[0].text, k[0].kurz, k[0].param], ['familie.aktualisieren', 'Aktualisieren …', '↻', { familie: 'FAM-1' }]);
  assert.deepEqual(T.standKnoepfe(projektFam('FAM-2', 'Y', 'Ventil', 'freigegeben', stand('aktuell'))), []);
  assert.match(T.standKnoepfe(projektFam('FAM-3', 'Z', 'Ventil', 'entwurf', stand('parallel_geaendert')))[0].titel, /parallel/);
});

test('Projektbrowser: Familien des Projekts tragen die Marken des Standes (neben Entwurf), der Knopf «Prüfen» bleibt', () => {
  const frei = B.familienKnoten(projektFam('FAM-1', 'Pumpe', 'Pumpe', 'freigegeben', stand('neuere_version')));
  assert.deepEqual(frei.marken, ['neuere Version']);
  assert.equal(frei.marke, 'neuere Version');
  assert.deepEqual(frei.knoepfe.map((x) => x.aktion), ['familie.aktualisieren']);
  assert.equal(frei.knopf.aktion, 'familie.aktualisieren');
  assert.match(frei.css, /bib-stand/);
  assert.match(frei.tipp, /Bibliothek: Text neuere_version/);
  const entwurf = B.familienKnoten(projektFam('FAM-2', 'Ventil', 'Ventil', 'entwurf', stand('parallel_geaendert')));
  assert.deepEqual(entwurf.marken, ['Entwurf', 'parallel geändert']);
  assert.equal(entwurf.marke, 'Entwurf', 'die erste Marke bleibt wie bisher');
  assert.deepEqual(entwurf.knoepfe.map((x) => x.aktion), ['familie.entwurf-pruefen-browser', 'familie.aktualisieren']);
  assert.equal(entwurf.knopf.aktion, 'familie.entwurf-pruefen-browser', 'der erste Knopf bleibt «Prüfen»');
  const warte = B.familienKnoten(projektFam('FAM-3', 'Q', 'Ventil', 'freigegeben', stand('im_projekt_geaendert', { warteschlange: true })));
  assert.deepEqual(warte.marken, ['noch nicht gespeichert']);
  assert.deepEqual(warte.knoepfe, []);
  const alt = B.familienKnoten(projektFam('FAM-4', 'R', 'Ventil', 'freigegeben', stand('aktuell')));
  assert.deepEqual(alt.marken, []); assert.equal(alt.marke, ''); assert.equal(alt.knopf, undefined);
  const fehler = B.familienKnoten(projektFam('FAM-5', 'S', 'Ventil', 'entwurf', stand('neuere_version'), { gueltig: false, definitionsfehler: 2 }));
  assert.deepEqual(fehler.marken, ['Fehler', 'neuere Version']);
  assert.equal(B.familienKnoten({ kennzeichen: 'FAM-6', name: 'T', kategorie: 'Ventil', status: 'freigegeben', typen: [], version: 1 }).marke, '', 'ältere Antworten ohne bibliothekStand');
});

test('Projektbrowser: Bibliotheksfamilien (nach Auswahl, nicht im Projekt) grau mit Marke «Bibliothek», Entwurf und Doppel; je Kategorie zwischen den Projektfamilien', () => {
  const liste = [projektFam('FAM-1', 'Heizkörper alt', 'Heizkoerper', 'freigegeben', stand('aktuell'), { id: 'a1' })];
  const bib = BIB.map((f) => ({ ...f, sichtbar: ['a1', 'a2', 'a3', 'm1'].includes(f.id) }));   // der Kern entscheidet: a1 ist im Projekt
  bib[0].imProjekt = 'FAM-1';
  bib[1].doppel = ['a1'];
  const ast = B.familienAst(liste, { key: 'f:katalog', label: 'Katalog', kinder: [], art: 'ast' }, (kz) => `id-${kz}`, bib);
  assert.deepEqual(ast.kinder.map((k) => k.label), ['Heizkoerper', 'Rohr', 'Speicher', 'Katalog']);
  assert.equal(ast.zahl, 4, '1 Projektfamilie + 3 sichtbare Bibliotheksfamilien (a1 ist schon im Projekt, a4/a5 sind nicht sichtbar)');
  const heiz = ast.kinder[0];
  assert.deepEqual(heiz.kinder.map((k) => [k.art, k.label]), [['bib-fam', 'Heizkörper Typ 33'], ['fam', 'Heizkörper alt']].sort((a, b) => a[1].localeCompare(b[1], 'de')));
  const k33 = heiz.kinder.find((k) => k.art === 'bib-fam');
  assert.deepEqual(k33.marken, ['Bibliothek', 'Entwurf', 'Doppel?']);
  assert.equal(k33.marke, 'Bibliothek');
  assert.equal(k33.bibId, 'a2'); assert.equal(k33.bibDatei, undefined);
  assert.match(k33.css, /bibliothek/); assert.match(k33.css, /entwurf/);
  assert.match(k33.tipp, /Doppelklick lädt/); assert.match(k33.tipp, /Doppel/);
  assert.equal(k33.kinder, undefined, 'Bibliotheksfamilien haben keine Typen im Baum (erst nach dem Laden)');
  const rohr = ast.kinder[1].kinder[0];
  assert.equal(rohr.bibDatei, 'test_rohr.familie.json'); assert.equal(rohr.bibId, undefined);
  assert.deepEqual(rohr.marken, ['Bibliothek']);
  assert.match(rohr.tipp, /Mitgeliefert/);
  // ohne Bibliothek bleibt alles wie in C1
  assert.equal(B.familienAst(liste, null, undefined, undefined).zahl, 1);
  assert.equal(B.familienAst([], null, undefined, []).kinder.length, 0);
  // Knoten «Familien»: zwei Knöpfe, der erste bleibt «Unterlagen»
  assert.deepEqual(ast.knoepfe.map((x) => x.aktion), ['familie.aus-unterlagen', 'familie.explorer']);
  assert.equal(ast.knopf.aktion, 'familie.aus-unterlagen');
});

test('Marken: eigene CSS-Klasse je Marke', () => {
  for (const [m, k] of [['Bibliothek', 'm-bib'], ['neuere Version', 'm-neu'], ['parallel geändert', 'm-par'], ['noch nicht gespeichert', 'm-wart'], ['Doppel?', 'm-doppel'], ['Fehler', 'm-fehler']]) assert.equal(T.markeKlasse(m), k);
  assert.equal(T.markeKlasse('Entwurf'), '');
});

// ================================================================================================ Auswahl «Familien im Explorer»

test('Auswahl lesen und schreiben: null = nur Projekt, leere Listen entfallen', () => {
  assert.deepEqual(T.auswahlParameter(T.auswahlLesen(null)), { auswahl: null });
  assert.deepEqual(T.auswahlParameter(T.auswahlLesen(undefined)), { auswahl: null });
  const leer = T.auswahlLesen({});
  assert.equal(leer.an, true);
  assert.deepEqual(T.auswahlParameter(leer), { auswahl: {} });
  const a = { gewerke: ['heizung'], kategorien: [], status: ['freigegeben'], ein: ['x1'], aus: [] };
  assert.deepEqual(T.auswahlParameter(T.auswahlLesen(a)), { auswahl: { gewerke: ['heizung'], status: ['freigegeben'], ein: ['x1'] } });
});

test('Sichtbar (Spiegelung der Kernregel): aus vor ein vor Filter, Gross/klein egal, fehlender Status = Entwurf, fehlender Wert trifft keinen Filter', () => {
  const f = BIB[0];   // a1: heizung, Heizkoerper, Fiktiv AG, freigegeben
  assert.equal(T.sichtbarNach(T.auswahlLesen(null), f), false, 'ohne Auswahl nur die Familien des Projekts');
  assert.equal(T.sichtbarNach(T.auswahlLesen({}), f), true, 'leere Listen schränken nicht ein');
  assert.equal(T.sichtbarNach(T.auswahlLesen({ gewerke: ['HEIZUNG'] }), f), true);
  assert.equal(T.sichtbarNach(T.auswahlLesen({ gewerke: ['sanitaer'] }), f), false);
  assert.equal(T.sichtbarNach(T.auswahlLesen({ gewerke: ['heizung'], status: ['entwurf'] }), f), false, 'alle nicht leeren Listen müssen treffen');
  assert.equal(T.sichtbarNach(T.auswahlLesen({ hersteller: ['fiktiv ag'] }), f), true);
  assert.equal(T.sichtbarNach(T.auswahlLesen({ hersteller: ['Fiktiv AG'] }), BIB[3]), false, 'ohne Hersteller trifft ein Herstellerfilter nicht');
  assert.equal(T.sichtbarNach(T.auswahlLesen({ status: ['entwurf'] }), { ...f, status: null }), true, 'Status null gilt als Entwurf');
  assert.equal(T.sichtbarNach(T.auswahlLesen({ gewerke: ['sanitaer'], ein: ['A1'] }), f), true, 'ein übersteuert die Filter');
  assert.equal(T.sichtbarNach(T.auswahlLesen({ gewerke: ['heizung'], aus: ['a1'] }), f), false, 'aus übersteuert die Filter');
  assert.equal(T.sichtbarNach(T.auswahlLesen({ ein: ['a1'], aus: ['a1'] }), f), false, 'aus gewinnt vor ein');
  assert.equal(T.sichtbarNach(T.auswahlLesen({}), { ...f, id: null }), true, 'ohne id nur die Filter');
});

test('Einzelnes Häkchen: übersteuert die Filter; passt der Wunsch zum Filter, entfällt der Eintrag', () => {
  let s = T.auswahlLesen({ gewerke: ['heizung'] });
  const heiz = BIB[0], sani = BIB[3];
  s = T.einzelnSetzen(s, heiz, false);
  assert.deepEqual(s.aus, ['a1']); assert.deepEqual(s.ein, []);
  assert.equal(T.sichtbarNach(s, heiz), false);
  s = T.einzelnSetzen(s, heiz, true);
  assert.deepEqual(s.aus, [], 'wieder wie der Filter: kein Eintrag mehr'); assert.deepEqual(s.ein, []);
  s = T.einzelnSetzen(s, sani, true);
  assert.deepEqual(s.ein, ['a4']);
  assert.equal(T.sichtbarNach(s, sani), true);
  s = T.einzelnSetzen(s, sani, false);
  assert.deepEqual(s.ein, []); assert.deepEqual(s.aus, []);
  assert.equal(T.einzelnSetzen(s, { name: 'ohne id' }, true), s, 'ohne id nichts zu setzen');
  // Wechsel des Filters nach einem Häkchen: der Einzelwunsch bleibt
  let t = T.einzelnSetzen(T.auswahlLesen({}), sani, false);
  t = T.filterUmschalten(t, 'gewerke', 'sanitaer');
  assert.equal(T.sichtbarNach(t, sani), false);
});

test('Filter: Chips umschalten (Gross/klein egal), Werte aus den Familien dedupliziert und sortiert, Status immer beide', () => {
  let s = T.auswahlLesen({});
  s = T.filterUmschalten(s, 'hersteller', 'Muster GmbH');
  assert.deepEqual(s.hersteller, ['Muster GmbH']);
  s = T.filterUmschalten(s, 'hersteller', 'muster gmbh');
  assert.deepEqual(s.hersteller, []);
  assert.deepEqual(T.filterWerte(BIB, 'gewerke').map((w) => [w.wert, w.text, w.anzahl]), [['heizung', 'Heizung', 3], ['lueftung', 'Lüftung', 1], ['sanitaer', 'Sanitär', 2]]);
  assert.deepEqual(T.filterWerte(BIB, 'hersteller').map((w) => [w.text, w.anzahl]), [['Fiktiv AG', 2], ['Muster GmbH', 2]], 'Fiktiv AG und fiktiv ag zählen zusammen; ohne Hersteller entfällt');
  assert.deepEqual(T.filterWerte(BIB, 'status').map((w) => [w.wert, w.anzahl]), [['entwurf', 2], ['freigegeben', 4]]);
  assert.deepEqual(T.filterWerte(BIB, 'kategorien').map((w) => w.wert), ['Heizkoerper', 'Rohr', 'Speicher', 'Waschtisch', 'Zuluftdurchlass']);
  assert.deepEqual(T.filterWerte([], 'gewerke'), []);
  assert.equal(T.gewerkName('sanitaer'), 'Sanitär'); assert.equal(T.gewerkName('kaelte'), 'Kälte'); assert.equal(T.gewerkName('neu'), 'Neu'); assert.equal(T.gewerkName(null), '–');
});

test('Liste im Dialog: nach Kategorie und Name, Häkchen nach der Auswahl, Familien des Projekts fest angehakt, ohne Auswahl alles fest', () => {
  const s = T.auswahlLesen({ gewerke: ['heizung'], aus: ['a2'], ein: ['a4'] });
  const z = T.explorerZeilen(BIB, s);
  assert.deepEqual(z.map((x) => x.id), ['a1', 'a2', 'm1', 'a3', 'a4', 'a5'].sort((p, q) => {
    const fp = BIB.find((f) => f.id === p), fq = BIB.find((f) => f.id === q);
    return fp.kategorie.localeCompare(fq.kategorie, 'de') || fp.name.localeCompare(fq.name, 'de');
  }));
  const nach = Object.fromEntries(z.map((x) => [x.id, x]));
  assert.equal(nach.a1.haken, true); assert.equal(nach.a1.fest, false);
  assert.equal(nach.a2.haken, false); assert.equal(nach.a2.uebersteuert, true);
  assert.equal(nach.a3.haken, true);
  assert.equal(nach.a4.haken, true); assert.equal(nach.a4.uebersteuert, true);
  assert.equal(nach.a5.haken, true); assert.equal(nach.a5.fest, true, 'im Projekt: immer sichtbar'); assert.equal(nach.a5.imProjekt, 'FAM-9');
  assert.equal(nach.m1.haken, false, 'mitgeliefert, Sanitär: Filter Heizung trifft nicht');
  const aus = T.explorerZeilen(BIB, T.auswahlLesen(null));
  assert.ok(aus.every((x) => x.fest), 'ohne Auswahl sind alle Häkchen gesperrt');
  assert.ok(aus.filter((x) => !x.imProjekt).every((x) => !x.haken));
  assert.deepEqual(T.explorerZeilen(BIB, s, 'speicher').map((x) => x.id), ['a3'], 'Suche nur in der Anzeige');
  assert.deepEqual(T.explorerZeilen(BIB, s, 'muster').map((x) => x.id).sort(), ['a3', 'a5'], 'Suche auch im Hersteller');
  assert.equal(T.explorerZeilen([], s).length, 0);
});

test('Zahl der sichtbaren: Familien des Projekts zählen nicht mit, Text nennt sie extra', () => {
  const z = T.explorerZahlen(BIB, T.auswahlLesen({ gewerke: ['heizung'] }));
  assert.deepEqual(z, { gesamt: 6, imProjekt: 1, sichtbar: 3, ausserhalb: 5 });
  assert.equal(T.zahlenText(z), '3 von 5 Bibliotheksfamilien sichtbar – dazu immer die 1 Familie des Projekts');
  assert.equal(T.zahlenText({ gesamt: 3, imProjekt: 0, sichtbar: 0, ausserhalb: 3 }), '0 von 3 Bibliotheksfamilien sichtbar');
  assert.equal(T.zahlenText({ gesamt: 5, imProjekt: 2, sichtbar: 5, ausserhalb: 3 }).endsWith('die 2 Familien des Projekts'), true);
  assert.equal(T.explorerZahlen(BIB, T.auswahlLesen(null)).sichtbar, 0);
});

test('Dialog-HTML: Chips (gesperrt ohne Auswahl), Liste mit Häkchen, Fuss mit «gilt für alle», Doppel und Antwort des Kerns; alles maskiert', () => {
  const s = T.auswahlLesen({ gewerke: ['heizung'] });
  const f = T.explorerFilterHtml(BIB, s);
  assert.match(f, /data-feld="gewerke" data-wert="heizung" aria-pressed="true"/);
  assert.match(f, /data-feld="gewerke" data-wert="sanitaer" aria-pressed="false"/);
  assert.equal((f.match(/class="bib-filter"/g) ?? []).length, 4);
  assert.doesNotMatch(f, /disabled/);
  assert.match(T.explorerFilterHtml(BIB, T.auswahlLesen(null)), /disabled/);
  const l = T.explorerListeHtml(T.explorerZeilen(BIB, s), s);
  assert.match(l, /<input type="checkbox" data-id="a1" checked aria-label=/);
  assert.match(l, /data-id="a5" checked disabled aria-label=/);
  assert.match(l, /im Projekt \(FAM-9\) – immer sichtbar/);
  assert.match(l, /mitgeliefert/);
  assert.match(T.explorerListeHtml([], s), /Keine Familien/);
  const boese = [{ ...BIB[0], name: '<b>x</b> & "y"', id: 'z"1' }];
  const lb = T.explorerListeHtml(T.explorerZeilen(boese, s), s);
  assert.doesNotMatch(lb, /<b>x<\/b>/); assert.match(lb, /&lt;b&gt;x&lt;\/b&gt; &amp; &quot;y&quot;/); assert.match(lb, /data-id="z&quot;1"/);
  const d = T.doppelTexte([{ hersteller: 'Fiktiv AG', produkt: 'Typ 22', familien: [{ id: 'a1', name: 'Heizkörper', quelle: 'bibliothek' }, { id: 'm1', name: 'Heizkörper (alt)', quelle: 'mitgeliefert' }] }]);
  assert.deepEqual(d, ['Fiktiv AG · Typ 22: «Heizkörper» und «Heizkörper (alt)» (mitgeliefert)']);
  const fuss = T.explorerFussHtml({ zahlen: T.explorerZahlen(BIB, s), kernHinweis: 'Familienauswahl gesetzt: 3 von 5', doppel: d });
  assert.ok(fuss.includes(T.GILT_FUER_ALLE) && /Gilt für alle, die an diesem Projekt arbeiten\./.test(fuss));
  assert.match(fuss, /3 von 5 Bibliotheksfamilien sichtbar/); assert.match(fuss, /Kern: Familienauswahl gesetzt/); assert.match(fuss, /Mögliche Doppel/); assert.match(fuss, /Fiktiv AG · Typ 22/);
  assert.match(T.explorerFussHtml({ zahlen: T.explorerZahlen(BIB, s), kernFehler: 'auswahl.status «x» gibt es nicht. – Erlaubt: entwurf, freigegeben.' }), /class="bib-kernfehler">auswahl\.status/);
  const aus0 = T.explorerFussHtml({ zahlen: T.explorerZahlen(BIB, T.auswahlLesen(null)), an: false });
  assert.match(aus0, /0 von 5 Bibliotheksfamilien sichtbar/); assert.match(aus0, /Auswahl aus: der Explorer zeigt nur die Familien des Projekts/);
});

// ================================================================================================ Aktualisieren

test('Wege je Stand: neuere Version einer, parallel geändert zwei (der zweite mit Warnung), noch nicht gespeichert zwei, sonst keiner', () => {
  assert.deepEqual(T.wegeFuer(stand('neuere_version')).map((w) => [w.id, w.befehl]), [['uebernehmen', 'familie_aktualisieren']]);
  const par = T.wegeFuer(stand('parallel_geaendert'));
  assert.deepEqual(par.map((w) => [w.id, w.befehl, !!w.alsAktuell, !!w.warnung]), [['uebernehmen', 'familie_aktualisieren', false, false], ['eigene', 'familie_in_bibliothek', true, true]]);
  assert.equal(par[0].text, 'Bibliothek übernehmen'); assert.equal(par[1].text, 'Meine Fassung wird aktuell');
  assert.deepEqual(T.wegeFuer(stand('im_projekt_geaendert')).map((w) => w.id), ['uebernehmen', 'schreiben']);
  assert.match(T.wegeFuer(stand('im_projekt_geaendert'))[0].text, /meine Fassung wird ersetzt/);
  assert.deepEqual(T.wegeFuer(stand('nicht_in_bibliothek')).map((w) => w.id), ['schreiben']);
  for (const art of ['aktuell', 'gesperrt', 'nicht_im_projekt']) assert.deepEqual(T.wegeFuer(stand(art)), [], art);
  assert.deepEqual(T.wegeFuer(null), []);
  assert.match(T.standErklaerung(stand('neuere_version', { version: 2 })), /neuere Version \(3\); das Projekt hat Version 2.*erst übernommen, wenn du es hier bestätigst/);
  assert.match(T.standErklaerung(stand('parallel_geaendert')), /parallel geändert.*entscheidest du/);
  assert.match(T.standErklaerung(stand('im_projekt_geaendert', { warteschlange: true })), /Warteschlange/);
});

test('Parameter der Befehle: version nur wenn gewählt, als_aktuell nur für «Meine Fassung wird aktuell»', () => {
  assert.deepEqual(T.aktualisierenParameter('FAM-1', ''), { familie: 'FAM-1' });
  assert.deepEqual(T.aktualisierenParameter('FAM-1', 4), { familie: 'FAM-1', version: '4' });
  assert.deepEqual(T.aktualisierenParameter('FAM-1', 'v0004-b-2026'), { familie: 'FAM-1', version: 'v0004-b-2026' });
  assert.deepEqual(T.schreibenParameter('FAM-1', true), { familien: ['FAM-1'], als_aktuell: true });
  assert.deepEqual(T.schreibenParameter('FAM-1', false), { familien: ['FAM-1'] });
});

const DETAIL = {
  id: 'a1', version: 5,
  versionen: [1, 2, 3, 4, 5].map((v) => ({ version: v, aktuell: v === 5, status: v === 5 ? 'freigegeben' : 'entwurf', zeit: `2026-10-1${v}T08:30:00.000Z`, projekt: v === 5 ? 'Musterweg' : 'Beispiel', akteurArt: v === 4 ? 'ki' : 'mensch' })).reverse(),
  abzweige: [{ abzweig: 'v0004-bern-1', offen: true, projekt: 'Bern', zeit: '2026-10-15T09:00:00.000Z' }, { abzweig: 'v0002-alt', offen: false }],
};

test('Versionswahl: nur Versionen, die neuer sind als die des Projekts; die aktuelle trägt den Wert «» (Vorgabe des Kerns)', () => {
  const o = T.versionsOptionen(stand('neuere_version', { version: 3 }), DETAIL);
  assert.deepEqual(o.map((x) => x.wert), ['', '4', 'v0004-bern-1'], 'dazu die offene parallele Fassung eines anderen Projekts');
  assert.match(o[0].text, /^Version 5 \(aktuell\) · freigegeben · Musterweg/);
  assert.match(o[1].text, /^Version 4 · Beispiel · ki · /);
  assert.match(o[0].text, /\d\d\.\d\d\.2026 \d\d:\d\d/);
  // eigene Fassung / Abzweig im Projekt: alle Versionen
  assert.deepEqual(T.versionsOptionen(stand('parallel_geaendert', { version: null, abzweig: 'v0004-meins' }), DETAIL).map((x) => x.wert), ['', '4', '3', '2', '1', 'v0004-bern-1']);
  // aktuell im Projekt, aber andere haben offene Abzweige: nur deren Abzweige zur Wahl
  const nurAbzweig = T.versionsOptionen(stand('parallel_geaendert', { version: 5 }), DETAIL);
  assert.deepEqual(nurAbzweig.map((x) => [x.wert, x.art]), [['v0004-bern-1', 'abzweig']]);
  assert.match(nurAbzweig[0].text, /Parallele Fassung v0004-bern-1 · Bern/);
  assert.deepEqual(T.versionsOptionen(stand('neuere_version', { version: 5 }), { ...DETAIL, abzweige: [] }), [], 'nichts Neueres');
  assert.deepEqual(T.versionsOptionen(null, null), []);
});

const VORSCHAU = {
  titel: 'Projekt = Version 2 → Bibliothek Version 3', zusammenfassung: 'Vorschau FAM-1 «Heizkörper» (Projekt = Version 2 → Bibliothek Version 3): 1 Kopfangabe(n), 1 Parameter, 1 Typ(en), 0 Körper, 1 Anschluss/Anschlüsse geändert; 2 von 3 Element(en) betroffen; 1 Element(e) mit entfallendem Typ.',
  kopf: [{ feld: 'hersteller', alt: 'Alt AG', neu: 'Neu AG' }],
  parameter: [{ name: 'tiefe', art: 'geaendert', aenderungen: ['wert 100 → 110'] }],
  typen: [{ name: '22/600/1000', art: 'entfaellt' }, { name: '22/600/1200', art: 'neu' }],
  koerper: [], anschluesse: [{ name: 'VL', art: 'geaendert', aenderungen: ['lage [10,0,0] → [22,0,0]'] }],
  weitere: ['sinnbild', 'unterlagen'],
  elemente: [{ element: 'HK-1', typ: '22/600/1000', meldungen: ['Typ «22/600/1000» entfällt'] }, { element: 'HK-2', typ: '22/600/1400', meldungen: ['Anschluss VL verschiebt sich um 12 mm (verbunden – Leitung prüfen)'] }],
  fehlendeTypen: [{ typ: '22/600/1000', elemente: ['HK-1'] }],
  zeilen: ['Vorschau FAM-1 …', 'Kopf: hersteller «Alt AG» → «Neu AG»', 'HK-2: Anschluss VL verschiebt sich um 12 mm (verbunden – Leitung prüfen)'],
};

test('Vorschau: Änderungszeilen aus der strukturierten Antwort, Elementtabelle, entfallende Typen und Warnungen des Trockenlaufs', () => {
  const z = T.aenderungsZeilen(VORSCHAU);
  assert.deepEqual(z, [
    'Kopf: hersteller «Alt AG» → «Neu AG»', 'Parameter «tiefe»: wert 100 → 110', 'Typ «22/600/1000»: entfällt', 'Typ «22/600/1200»: neu', 'Anschluss «VL»: lage [10,0,0] → [22,0,0]',
    'Sinnbild geändert', 'Unterlagen geändert']);
  assert.deepEqual(T.aenderungsZeilen(null), []);
  const h = T.vorschauHtml({ zusammenfassung: VORSCHAU.zusammenfassung, vorschau: VORSCHAU, hinweise: [VORSCHAU.zusammenfassung, ...VORSCHAU.zeilen.slice(1), 'Achtung: die Fassung von FAM-1 im Projekt ist nicht in der Bibliothek – sie wird ersetzt.'] });
  assert.match(h, /<p class="bib-zus">Vorschau FAM-1 «Heizkörper»/);
  assert.match(h, /Änderungen \(7\)/);
  assert.match(h, /Betroffene Elemente \(2\)/);
  assert.match(h, /<td>HK-2<\/td><td>22\/600\/1400<\/td><td>Anschluss VL verschiebt sich um 12 mm/);
  assert.match(h, /Entfallende Typen, die benutzt werden/); assert.match(h, /Typ «22\/600\/1000» – 1 Element \(HK-1\)/);
  assert.match(h, /<div class="bib-warn"><p>Achtung: die Fassung von FAM-1/, 'Achtung steht als Warnung');
  const n = T.vorschauHtml({ zusammenfassung: 'Z', hinweise: ['Z', 'FAM-1 «X» aus der Familienbibliothek übernommen: Version 3, Status entwurf.'] });
  assert.match(n, /bib-notizen.*Weitere Hinweise des Kerns.*aus der Familienbibliothek übernommen/); assert.doesNotMatch(n, /bib-warn/, 'andere Hinweise sind keine Warnung');
  assert.equal((h.match(/Anschluss VL verschiebt sich/g) ?? []).length, 1, 'Elementzeilen nicht doppelt (nur in der Tabelle)');
  assert.doesNotMatch(h, /Kopf: hersteller «Alt AG» → «Neu AG»<\/p>/, 'bekannte Zeilen des Kerns stehen nicht noch einmal als Warnung');
  const leer = T.vorschauHtml({ zusammenfassung: 'Keine Änderung: FAM-1 entspricht schon Version 3 der Bibliothek.', vorschau: { ...VORSCHAU, kopf: [], parameter: [], typen: [], koerper: [], anschluesse: [], weitere: [], elemente: [], fehlendeTypen: [], zeilen: [] } });
  assert.match(leer, /Keine Änderungen an Kopf/); assert.match(leer, /Kein platziertes Element ändert sich/);
  assert.match(T.vorschauHtml({ vorschau: VORSCHAU }), /Vorschau FAM-1/, 'Zusammenfassung notfalls aus der Vorschau');
  assert.equal(T.vorschauHtml({}), '');
  assert.doesNotMatch(T.vorschauHtml({ zusammenfassung: '<img src=x onerror=1>' }), /<img/);
  const a = T.ablehnungText({ fehler: 'FAM-1 lässt sich nicht aktualisieren: Typ «x» entfällt – 3 Element(e).', vorschlag: 'Die Elemente zuerst umstellen.' });
  assert.deepEqual(a, { fehler: 'FAM-1 lässt sich nicht aktualisieren: Typ «x» entfällt – 3 Element(e).', vorschlag: 'Die Elemente zuerst umstellen.' });
  assert.deepEqual(T.ablehnungText({}), { fehler: 'Abgelehnt.', vorschlag: '' });
});

// ================================================================================================ Familie laden

test('Familie laden: Liste aus familien_bibliothek (Bibliothek vor mitgeliefert), Parameter für id, mitgelieferte Datei, Version und freies Feld', () => {
  const z = T.ladenZeilen(BIB);
  assert.equal(z.length, 6);
  assert.deepEqual(z.map((x) => x.quelle), ['bibliothek', 'bibliothek', 'bibliothek', 'bibliothek', 'bibliothek', 'mitgeliefert']);
  assert.deepEqual(z.slice(0, 2).map((x) => x.id), ['a1', 'a2']);
  assert.deepEqual(T.ladenZeilen(BIB, 'rohr').map((x) => x.name), ['Testrohr']);
  assert.deepEqual(T.ladenZeilen([{ quelle: 'bibliothek', name: 'ohne Kennung' }]), []);
  const bib = z.find((x) => x.id === 'a1'), mit = z.find((x) => x.id === 'm1');
  assert.deepEqual(T.ladenParameter({ zeile: bib }), { id: 'a1' });
  assert.deepEqual(T.ladenParameter({ zeile: bib, version: '2' }), { id: 'a1', version: '2' });
  assert.deepEqual(T.ladenParameter({ zeile: mit, version: '2' }), { datei: 'test_rohr.familie.json' }, 'mitgelieferte Dateien haben keine Versionen');
  assert.deepEqual(T.ladenParameter({ zeile: bib, pfad: ' C:\\X\\a.familie.json ' }), { datei: 'C:\\X\\a.familie.json' }, 'das freie Feld gewinnt');
  assert.equal(T.ladenParameter({}), null);
  const h = T.ladenListeHtml(z, bib.key);
  assert.match(h, /class="bib-lade gewaehlt"/); assert.match(h, /name="bib-lade" checked/); assert.match(h, /mitgeliefert/);
  assert.match(T.ladenListeHtml(T.ladenZeilen([{ ...BIB[0], imProjekt: 'FAM-7' }]), ''), /im Projekt \(FAM-7\), wird aktualisiert/);
  assert.match(T.ladenListeHtml([], ''), /Keine Familien/);
});

// ================================================================================================ Statusleiste

const INFO = { pfad: 'C:\\Temp\\bib', quelle: 'Umgebungsvariable HLKS_FAMILIENBIBLIOTHEK', aktiv: true, gesperrt: null, erreichbar: true, offline: false, stand: '2026-10-19T08:00:00.000Z', warteschlange: 0, nachgeholt: 0, hinweis: null, fehler: null };

test('Statusleiste: erreichbar, offline, Warteschlange, gesperrt, ohne Projekt', () => {
  assert.deepEqual(T.bibliothekStatus(INFO), { text: 'Bibliothek erreichbar', klasse: 'gruen', titel: 'Familienbibliothek C:\\Temp\\bib – klicken für Einzelheiten.' });
  const warte = T.bibliothekStatus({ ...INFO, warteschlange: 2 });
  assert.equal(warte.text, 'Bibliothek erreichbar · 2 in Warteschlange'); assert.equal(warte.klasse, 'warn');
  const off = T.bibliothekStatus({ ...INFO, offline: true, erreichbar: false, warteschlange: 1 });
  assert.equal(off.text, 'Bibliothek offline · 1 in Warteschlange'); assert.equal(off.klasse, 'rot'); assert.match(off.titel, /nicht erreichbar \(C:\\Temp\\bib\)/);
  assert.equal(T.bibliothekStatus({ ...INFO, offline: true }).text, 'Bibliothek offline');
  const g = T.bibliothekStatus({ ...INFO, aktiv: false, gesperrt: 'Testlauf ohne HLKS_FAMILIENBIBLIOTHEK' });
  assert.equal(g.text, 'Bibliothek gesperrt'); assert.equal(g.klasse, 'leise'); assert.match(g.titel, /Testlauf/);
  assert.equal(T.bibliothekStatus(null), null);
});

test('Hinweis zur Bibliothek: Pfad, Quelle, Zustand, Warteschlange, nachgeholt, Hinweis des Kerns', () => {
  const h = T.bibliothekHinweisHtml({ ...INFO, nachgeholt: 3, warteschlange: 0, hinweis: '3 ausstehende(r) Schreibvorgang/-vorgänge nachgeschrieben.', fehler: ['Index <x>'] });
  assert.match(h, /<th>Pfad<\/th><td>C:\\Temp\\bib<\/td>/);
  assert.match(h, /<th>Quelle<\/th><td>Umgebungsvariable HLKS_FAMILIENBIBLIOTHEK<\/td>/);
  assert.match(h, /<th>Zustand<\/th><td>erreichbar<\/td>/);
  assert.match(h, /<th>Nachgeholt<\/th><td>3 Schreibvorgang\/-vorgänge beim letzten Zugriff nachgeschrieben<\/td>/);
  assert.match(h, /<th>In der Warteschlange<\/th><td>0<\/td>/);
  assert.match(h, /bib-hinweistext">3 ausstehende/);
  assert.match(h, /Index &lt;x&gt;/);
  assert.match(T.bibliothekHinweisHtml({ ...INFO, offline: true }), /nicht erreichbar \(letzter bekannter Stand\)/);
  assert.match(T.bibliothekHinweisHtml({ ...INFO }), /<th>Nachgeholt<\/th><td>nichts<\/td>/);
  assert.match(T.bibliothekHinweisHtml({ ...INFO, nachgeholtMerk: { anzahl: 2, zeit: '2026-10-19T08:00:00.000Z' } }), /<th>Nachgeholt<\/th><td>2 Schreibvorgang\/-vorgänge nachgeschrieben \(\d\d\.\d\d\.2026 \d\d:\d\d\)<\/td>/, 'die zuletzt gesehene Nachholung bleibt stehen');
  assert.match(T.bibliothekHinweisHtml(null), /Kein Projekt offen/);
  assert.match(T.zeitText('2026-10-19T08:00:00.000Z'), /^\d\d\.\d\d\.2026 \d\d:\d\d$/);
  assert.equal(T.zeitText('kaputt'), '');
});

// ================================================================================================ Verdrahtung

test('Verdrahtung: Menüband-Gruppe, Symbole, Aktionen, Projektbrowser, Statusleiste, CSS, Einbindung, Kopfkommentare', () => {
  const unbekannt = sym.symbol('gibtsnicht');
  for (const s of ['famBibliothek', 'famAktualisieren']) assert.notEqual(sym.symbol(s), unbekannt, `Symbol ${s}`);
  assert.match(/familie: \[([^\]]*)\]/.exec(lies('../oberflaeche/menueband.js'))[1], /'Unterlagen', 'Bibliothek'/);
  const akt = lies('aktionen-bibliothek.js');
  for (const id of ['familie.explorer', 'familie.explorer-register', 'familie.aktualisieren', 'familie.aktualisieren-register', 'familie.bibliothek-laden', 'familie.bibliothek-platzieren'])
    assert.ok(akt.includes(`id: '${id}'`), `Aktion ${id}`);
  assert.match(akt, /register: 'familie', gruppe: 'Bibliothek'/);
  assert.match(akt, /register: 'gebaeudetechnik'/);
  assert.match(lies('../oberflaeche/aktionen.js'), /import '\.\.\/familien\/aktionen-bibliothek\.js'/);
  const pb = lies('../oberflaeche/projektbrowser.js');
  for (const s of ["'bib-fam'", "'familie.bibliothek-platzieren'", "'familie.explorer'", "'familie.aktualisieren'", 'markenHtml', 'knoepfeHtml', 'data-param', 'bibliothekFamilien()']) assert.ok(pb.includes(s), s);
  const sl = lies('../oberflaeche/statusleiste.js');
  for (const s of ['sl-bib', 'bibliothekStatus', 'bibliothekHinweis']) assert.ok(sl.includes(s), s);
  assert.ok(pb.includes('pb-zusatz') && pb.includes('mit-zusatz'), 'Marken und Knöpfe im Rahmen pb-zusatz (zweite Zeile statt gekürztem Namen)');
  // «Familie laden» liest die Liste aus familien_bibliothek, nicht mehr aus dem Vorschlag einer unbekannten Datei
  const af = lies('aktionen-familien.js');
  assert.match(af, /ladenDialog/); assert.doesNotMatch(af, /__liste__/); assert.doesNotMatch(af, /bibliotheksDateien/);
  const dlg = lies('bibliothek-dialoge.js');
  for (const s of ["sende('familienauswahl_setzen'", "sende('familie_aktualisieren'", "sende('familie_in_bibliothek'", "sende('familie_laden'", '{ trockenlauf: true }']) assert.ok(dlg.includes(s), s);
  assert.ok(lies('bibliothek-daten.js').includes("abfrage('familien_bibliothek'"));
  // CSS: feste Farben nur in :root, Schriftgrössen 12/11/9, kein zoom, eingebunden
  const css = lies('../../css/familien-bibliothek.css').replace(/\/\*[\s\S]*?\*\//g, '');
  assert.deepEqual(css.replace(/:root(\[[^\]]*\])?\s*\{[^}]*\}/g, '').match(/#[0-9a-fA-F]{3,8}\b/g) ?? [], [], 'feste Farben');
  for (const m of css.matchAll(/font-size:\s*([\d.]+)px/g)) assert.ok(['12', '11', '9'].includes(m[1]), `Schriftgrösse ${m[1]}`);
  assert.doesNotMatch(css, /(^|[;{\s])zoom\s*:/);
  assert.match(lies('../../index.html'), /href="css\/familien-bibliothek\.css"/);
  for (const k of ['m-bib', 'm-neu', 'm-par', 'm-wart', 'm-doppel']) assert.ok(css.includes(`.pb-marke.${k}`), `Marke ${k} im CSS`);
  for (const k of ['gruen', 'rot', 'warn', 'leise']) assert.ok(css.includes(`.sl-bib.${k}`), `Statusfarbe ${k}`);
  assert.ok(css.includes('.pb-zusatz') && css.includes('.pb-zeile.mit-zusatz') && css.includes('flex-wrap: wrap'), 'Zeilen mit Marken umbrechen');
  // keine Fachlogik: kein Ändern von Elementen, keine Dateizugriffe
  for (const d of ['bibliothek-dialoge.js', 'bibliothek-daten.js', 'aktionen-bibliothek.js', 'bibliothek-text.js']) {
    assert.ok(!lies(d).includes('element_aendern'), d);
    assert.doesNotMatch(lies(d), /\bfetch\(|readFile|XMLHttpRequest/, d);
    assert.match(lies(d).split('\n')[0], /^\/\/ .{20,}/, `Kopfkommentar ${d}`);
  }
});

test('Der Kern kennt die Befehle, Abfragen und Parameter, die die Oberfläche sendet; die KI bekommt die Übernahme-Befehle nicht', () => {
  const kern = path.resolve(hier, '../../../../Hlks.Kern/Familien');
  if (!existsSync(kern)) return;
  const text = ['FamilienBibliothekBefehle.cs', 'FamilienBibliothek.cs', 'FamilienBefehle.cs'].map((f) => lies(path.join('../../../../Hlks.Kern/Familien', f))).join('\n');
  for (const n of ['familie_aktualisieren', 'familie_in_bibliothek', 'familienauswahl_setzen', 'familie_laden']) assert.ok(text.includes(`"${n}"`), `Kern kennt ${n}`);
  for (const p of ['als_aktuell', 'auswahl', 'familien', 'familie', 'version', 'id', 'datei', 'filter']) assert.ok(text.includes(`new("${p}"`), `Parameter ${p}`);
  for (const p of ['gewerke', 'kategorien', 'hersteller', 'status', 'ein', 'aus']) assert.ok(text.includes(`"${p}"`), `Auswahlfeld ${p}`);
  // Werte von stand.art, die die Oberfläche kennt
  for (const art of ['nicht_im_projekt', 'aktuell', 'neuere_version', 'im_projekt_geaendert', 'parallel_geaendert', 'nicht_in_bibliothek', 'gesperrt']) assert.ok(text.includes(`"${art}"`), `stand.art ${art}`);
  // Feldnamen der Antwort
  for (const f of ['"bibliothek"', '"auswahl"', '"anzahl"', '"familien"', '"doppel"', '"imProjekt"', '"stand"', '"sichtbar"', '"versionen"', '"abzweige"', '"vorschau"', '"warteschlange"', '"nachgeholt"', '"offline"', '"erreichbar"']) assert.ok(text.includes(f), `Feld ${f}`);
  const auftrag = path.resolve(hier, '../../../AuftragDienst.cs');
  if (existsSync(auftrag)) {
    const a = readFileSync(auftrag, 'utf8');
    const liste = /ErlaubteWerkzeuge =\s*\[([\s\S]*?)\];/.exec(a)[1];
    assert.ok(liste.includes('"familien_bibliothek"'));
    for (const n of ['familie_aktualisieren', 'familie_in_bibliothek', 'familienauswahl_setzen']) assert.ok(!liste.includes(`"${n}"`), `${n} nicht für die KI`);
  }
});
