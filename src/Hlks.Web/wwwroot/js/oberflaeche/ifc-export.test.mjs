// Tests des Dialogs «IFC exportieren» (Modul 7 Teil B2) ohne Browser: Anfrage an den Kern aus der Auswahl, Prüfung der Auswahl, Pfade und Links,
// Darstellung des Exportberichts (alles Dynamische maskiert, keine HTML-Einschleusung), Kennzeichen zum Anklicken, Fehlertexte, Verdrahtung.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/ifc-export.test.mjs
// Der Beispielbericht hat die Form der Antwort von POST /api/ifc/export (Kern: IfcExportBericht.Json); Namen und Werte sind fiktiv.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as T from './ifc-export-text.js';
import * as S from './symbole.js';
import * as RV from './symbole-revit.js';

const lies = (n) => readFileSync(new URL(n, import.meta.url), 'utf8');

const DATEI = 'C:\\Projekte\\Worb_Test\\c_Pläne und Modelle\\e_KICAD\\ausgabe\\ifc\\MFH Musterweg (Beispiel, fiktiv)_IFC4_20261020-1604.ifc';
const bericht = () => ({
  datei: DATEI, fassung: 'IFC4', einheit: 'mm', groesseBytes: 2411234, entitaetenDatei: 5786, dauerSekunden: 2.29,
  dauer: { aufbauUndSchreiben: 0.53, pruefen: 0.61, oeffnen: 0.09, schemapruefung: 0.48, ids: 0.04 },
  bericht: { json: DATEI.replace(/\.ifc$/, '.bericht.json'), md: DATEI.replace(/\.ifc$/, '.bericht.md') },
  ergebnis: { kontrolle: 'bestanden', schemapruefung: 'fehlerfrei', ids: 'nicht bestanden', idsRegelnNichtBestanden: 1 },
  anzahl: { elemente: 148, raeume: 0, ports: 298, verbindungen: 142, systeme: 4, straenge: 9, anlagen: 2, regelkreise: 1, typobjekte: 22, daemmungen: 0, geschosse: 3, uebersprungen: 2 },
  jeKlasse: [
    { klasse: 'Badewanne', entitaet: 'IfcFlowTerminal', typobjekt: 'IfcSanitaryTerminalType', predefinedType: 'BATH', anzahl: 2 },
    { klasse: 'Rohr', entitaet: 'IfcPipeSegment', typobjekt: 'IfcPipeSegmentType', predefinedType: null, anzahl: 71 },
  ],
  jeEntitaet: { IfcBuilding: 1, IfcPipeSegment: 71, IfcDistributionPort: 298 },
  uebersprungenJeGrund: { 'Gewerk msrl nicht im Umfang': 2 },
  uebersprungen: [{ element: 'AS-1', klasse: 'Automationsstation', grund: 'Gewerk msrl nicht im Umfang' }, { element: 'DP-MV1', klasse: 'Datenpunkt', grund: 'Gewerk msrl nicht im Umfang' }],
  schemapruefung: { ergebnis: 'fehlerfrei', werkzeug: 'Xbim 6.1.605', entitaetenGeprueft: 5786, meldungen: 0, beispiele: [] },
  kontrolle: { ergebnis: 'bestanden', produkte: 148, ports: 298, gruppen: 15, fassungGelesen: 'IFC4', einheitGelesen: 'mm', abweichungen: [] },
  ids: [
    { datei: 'C:\\kataloge\\ids\\hk-hlks-basis.ids', titel: 'H+K HLKS-Basis', basis: true, ergebnis: 'bestanden', xsdMeldungen: [], regeln: [
      { name: 'Rohre mit Nennweite bzw. Aussendurchmesser', ifcVersion: 'IFC2X3 IFC4', ergebnis: 'bestanden', anwendbar: 71, nichtBestandenElemente: 0, befunde: [] },
      { name: 'HLKS-Elemente in einem System (IFC2X3)', ifcVersion: 'IFC2X3', ergebnis: 'nicht anwendbar (Fassung)', grund: 'gilt für IFC2X3, die Datei ist IFC4' },
    ] },
    { datei: 'C:\\Daten\\Bauherr.ids', titel: 'Testregel Bauherr (fiktiv)', basis: false, ergebnis: 'nicht bestanden', xsdMeldungen: [], regeln: [
      { name: 'Rohre mit Freigabevermerk', ifcVersion: 'IFC2X3 IFC4', ergebnis: 'nicht bestanden', anwendbar: 71, nichtBestandenElemente: 71, grund: null, befunde: [
        { kennzeichen: 'KW-1.01-SP', globalId: '3XZ9P4a3DTNh5poVP6pZoC', entitaet: 'IfcPipeSegment', gruende: ['Eigenschaft «Pset_Bauherr».«Freigabe» fehlt'] },
        { kennzeichen: 'KW-1.02-1', globalId: 'x', entitaet: 'IfcPipeSegment', gruende: ['Eigenschaft «Pset_Bauherr».«Freigabe» fehlt'] },
      ] },
    ] },
  ],
  hinweise: ['4 Verbindung(en) zu nicht exportierten Elementen ausgelassen (Umfang, Grundlagen).'],
  warnungen: ['Rohre ohne Aussendurchmesser: Körper DN-gerecht wie das 3D: 69× – z. B. KW-1.01-SP.'],
});

// ------------------------------------------------------------------------------------------------ Auswahl und Anfrage

test('Ausgangsauswahl: IFC4, alle Gewerke und Geschosse, ohne Räume, keine weiteren IDS', () => {
  const a = T.auswahlStart();
  assert.deepEqual(a, { fassung: 'IFC4', gewerkeAlle: true, gewerke: [], geschosseAlle: true, geschosse: [], raeume: false, ids: '' });
  assert.deepEqual(T.FASSUNGEN.map((f) => f.id), ['IFC4', 'IFC2X3']);
  assert.equal(T.auswahlPruefen(a), true);
});

test('Anfrage aus der Vorgabe: nur fassung und raeume, kein umfang, keine ids; das Ziel bestimmt der Kern', () => {
  assert.deepEqual(T.anfrageAus(T.auswahlStart()), { fassung: 'IFC4', raeume: false });
  assert.ok(!('datei' in T.anfrageAus(T.auswahlStart())));
});

test('Anfrage mit Auswahl: Gewerke, Geschosse, Räume, IDS-Pfade; «alle» gewinnt über einzelne Häkchen', () => {
  const a = { ...T.auswahlStart(), fassung: 'IFC2X3', raeume: true, gewerkeAlle: false, gewerke: ['sanitaer', 'heizung'], geschosseAlle: false, geschosse: ['EG', '1.OG'],
    ids: 'C:\\Daten\\Bauherr.ids\n"C:\\Daten\\Architekt.ids"\r\n' };
  assert.deepEqual(T.anfrageAus(a), {
    fassung: 'IFC2X3', raeume: true, umfang: { gewerke: ['sanitaer', 'heizung'], geschosse: ['EG', '1.OG'] }, ids: ['C:\\Daten\\Bauherr.ids', 'C:\\Daten\\Architekt.ids'],
  });
  // «alle» angekreuzt: übrige Häkchen zählen nicht
  assert.deepEqual(T.anfrageAus({ ...a, gewerkeAlle: true, geschosseAlle: true, ids: '' }), { fassung: 'IFC2X3', raeume: true });
  // nur Gewerke eingeschränkt
  assert.deepEqual(T.anfrageAus({ ...a, geschosseAlle: true, ids: '' }).umfang, { gewerke: ['sanitaer', 'heizung'] });
  // die Anfrage ändert die Auswahl nicht
  const g = ['EG']; const b = { ...T.auswahlStart(), geschosseAlle: false, geschosse: g };
  T.anfrageAus(b).umfang.geschosse.push('X');
  assert.deepEqual(g, ['EG']);
});

test('IDS-Pfade: eine Datei je Zeile, ohne Leerraum und Anführungszeichen, ohne Doppelte (auch bei anderer Schreibweise der Buchstaben)', () => {
  assert.deepEqual(T.idsPfade(''), []);
  assert.deepEqual(T.idsPfade(null), []);
  assert.deepEqual(T.idsPfade('  \n\n'), []);
  assert.deepEqual(T.idsPfade(' "C:\\a b\\x.ids" \nC:\\A B\\X.ids\nD:\\y.ids'), ['C:\\a b\\x.ids', 'D:\\y.ids']);
});

test('Auswahl prüfen: ohne Häkchen und ohne «alle» fehlt etwas, mit Häkchen nicht; unbekannte Fassung wird abgelehnt', () => {
  const a = T.auswahlStart();
  assert.match(T.auswahlPruefen({ ...a, gewerkeAlle: false }), /Gewerk/);
  assert.match(T.auswahlPruefen({ ...a, geschosseAlle: false }), /Geschoss/);
  assert.equal(T.auswahlPruefen({ ...a, gewerkeAlle: false, gewerke: ['sanitaer'] }), true);
  assert.equal(T.auswahlPruefen({ ...a, geschosseAlle: false, geschosse: ['EG'] }), true);
  assert.match(T.auswahlPruefen({ ...a, fassung: 'IFC4X3' }), /Fassung/);
  assert.equal(T.auswahlPruefen({ ...a, fassung: 'IFC2X3' }), true);
});

test('Umfangstext: Fassung, Gewerke, Geschosse, Räume, IDS', () => {
  assert.equal(T.umfangText(T.auswahlStart()), 'IFC4 · alle Gewerke · alle Geschosse · ohne Räume');
  assert.equal(T.umfangText({ ...T.auswahlStart(), fassung: 'IFC2X3', raeume: true, gewerkeAlle: false, gewerke: ['a', 'b'], geschosseAlle: false, geschosse: ['EG', '1.OG'], ids: 'x.ids\ny.ids' }),
    'IFC2X3 · 2 Gewerke · EG, 1.OG · mit Räumen · 2 weitere IDS-Dateien');
  assert.match(T.umfangText({ ...T.auswahlStart(), gewerkeAlle: false, gewerke: ['a'], ids: 'x.ids' }), /1 Gewerk · .*1 weitere IDS-Datei$/);
});

// ------------------------------------------------------------------------------------------------ Pfade und Links

test('Dateiname, Ordner und Download-Adresse: Leerzeichen, Klammern und Umlaute werden kodiert, nur der Dateiname geht an den Server', () => {
  assert.equal(T.dateiName(DATEI), 'MFH Musterweg (Beispiel, fiktiv)_IFC4_20261020-1604.ifc');
  assert.equal(T.ordnerVon(DATEI), 'C:\\Projekte\\Worb_Test\\c_Pläne und Modelle\\e_KICAD\\ausgabe\\ifc');
  assert.equal(T.ordnerVon('x.ifc'), '');
  assert.equal(T.ordnerVon('/a/b/c.ifc'), '/a/b');
  const u = T.downloadUrl(DATEI);
  assert.ok(u.startsWith('/api/ifc/export/datei?pfad='));
  assert.equal(decodeURIComponent(u.split('pfad=')[1]), 'MFH Musterweg (Beispiel, fiktiv)_IFC4_20261020-1604.ifc');
  assert.ok(!u.includes('\\') && !u.includes(' '));
});

test('Zielhinweis: <Projektordner>\\ausgabe\\ifc, ohne bekannten Ordner der allgemeine Hinweis', () => {
  assert.equal(T.zielText('C:\\P\\e_KICAD'), 'C:\\P\\e_KICAD\\ausgabe\\ifc');
  assert.equal(T.zielText('C:\\P\\e_KICAD\\'), 'C:\\P\\e_KICAD\\ausgabe\\ifc');
  assert.equal(T.zielText(null), '<Projektordner>\\ausgabe\\ifc');
});

test('Grösse und Dauer', () => {
  assert.equal(T.groesseText(512), '512 Byte');
  assert.equal(T.groesseText(2048), '2,0 kB');
  assert.equal(T.groesseText(2411234), '2,3 MB');
  assert.equal(T.groesseText(undefined), '–');
  assert.equal(T.dauerText(2.294), '2,29 s');
  assert.equal(T.dauerText(12.34), '12,3 s');
  assert.equal(T.dauerText(null), '–');
});

// ------------------------------------------------------------------------------------------------ Fehler

test('Fehler: 409 kein Projekt, 400 mit Vorschlag des Kerns, sonst die Meldung; fehlerText hängt den Vorschlag an', () => {
  const k = T.fehlerAnzeige({ message: 'Kein Projekt offen.', status: 409 });
  assert.equal(k.titel, 'Kein Projekt offen');
  assert.match(k.vorschlag, /Projekt öffnen/);
  const e = T.fehlerAnzeige({ message: 'Nichts zu exportieren: alle 3 Elemente sind ausgelassen.', vorschlag: 'Umfang lockern', status: 400 });
  assert.equal(e.text, 'Nichts zu exportieren: alle 3 Elemente sind ausgelassen.');
  assert.equal(e.vorschlag, 'Umfang lockern');
  assert.equal(T.fehlerAnzeige({ message: 'kaputt', status: 500 }).titel, 'Nicht exportiert');
  assert.equal(T.fehlerAnzeige(new Error('x')).text, 'x');
  assert.equal(T.fehlerText({ message: 'a', vorschlag: 'b' }), 'a – b');
  assert.equal(T.fehlerText({ message: 'a' }), 'a');
});

// ------------------------------------------------------------------------------------------------ Bericht

test('Bericht: Datei mit Download- und Berichtslinks, Ergebnismarken, Dauer', () => {
  const h = T.kopfHtml(bericht());
  assert.match(h, /<b class="ie-name">MFH Musterweg \(Beispiel, fiktiv\)_IFC4_20261020-1604\.ifc<\/b>/);
  assert.match(h, /2,3 MB/);
  assert.match(h, /href="\/api\/ifc\/export\/datei\?pfad=MFH%20Musterweg%20\(Beispiel%2C%20fiktiv\)_IFC4_20261020-1604\.ifc"/);
  assert.match(h, /class="ie-bericht-md" href="[^"]*\.bericht\.md"/);
  assert.match(h, /class="ie-bericht-json" href="[^"]*\.bericht\.json"/);
  assert.match(h, /data-art="Schemaprüfung"><span class="ie-marke-t">Schemaprüfung<\/span> <b>fehlerfrei<\/b>/);
  assert.match(h, /ie-marke schlecht" data-art="IDS"/);
  assert.match(h, /ie-marke ok" data-art="Kontrolle beim Zurücklesen"/);
  assert.match(h, /Dauer <b class="ie-dauer-zahl">2,29 s<\/b>/);
});

test('Bericht: Zähler je Klasse und Entität, Übersicht mit allen Zählern', () => {
  const h = T.zaehlerHtml(bericht());
  assert.match(h, /<td>Badewanne<\/td><td>IfcFlowTerminal<\/td><td>IfcSanitaryTerminalType<\/td><td>BATH<\/td><td class="zahl">2<\/td>/);
  assert.match(h, /<td>Rohr<\/td><td>IfcPipeSegment<\/td><td>IfcPipeSegmentType<\/td><td>–<\/td><td class="zahl">71<\/td>/);
  assert.match(h, /data-zaehler="elemente">Elemente <b>148<\/b>/);
  assert.match(h, /data-zaehler="ports">Anschlüsse <b>298<\/b>/);
  assert.match(h, /Je IFC-Entität \(3\)/);
  assert.match(h, /<td>IfcDistributionPort<\/td><td class="zahl">298<\/td>/);
});

test('Bericht: übersprungene Elemente mit Grund und anklickbaren Kennzeichen; ohne sie «Keine.»', () => {
  const h = T.uebersprungenHtml(bericht());
  assert.match(h, /Übersprungene Elemente \(2\)/);
  assert.match(h, /data-grund="Gewerk msrl nicht im Umfang"><b>Gewerk msrl nicht im Umfang<\/b> \(2\)/);
  assert.match(h, /<button type="button" class="ie-kz" data-kz="AS-1" title="Automationsstation">AS-1<\/button>/);
  const leer = { ...bericht(), anzahl: { ...bericht().anzahl, uebersprungen: 0 }, uebersprungen: [], uebersprungenJeGrund: {} };
  assert.match(T.uebersprungenHtml(leer), /Keine\./);
});

test('Bericht: Schemaprüfung und Kontrolle, auch mit Meldungen', () => {
  assert.match(T.schemaHtml(bericht()), /ie-ergebnis ok">fehlerfrei<\/span> 5786 Entitäten geprüft, 0 Meldungen/);
  const b = bericht();
  b.schemapruefung = { ergebnis: 'Fehler', werkzeug: 'Xbim', entitaetenGeprueft: 10, meldungen: 2, beispiele: ['#5 IfcWall: Name fehlt'] };
  b.kontrolle = { ...b.kontrolle, ergebnis: 'abgewichen', abweichungen: ['KW-1: fehlt in der Datei'] };
  const h = T.schemaHtml(b);
  assert.match(h, /ie-ergebnis schlecht">Fehler<\/span>/);
  assert.match(h, /<li>#5 IfcWall: Name fehlt<\/li>/);
  assert.match(h, /ie-ergebnis schlecht">abgewichen<\/span>/);
  assert.match(h, /<li>KW-1: fehlt in der Datei<\/li>/);
});

test('Bericht: IDS je Datei und Regel – Basis gekennzeichnet, nicht bestandene Regeln offen mit Kennzeichen, nicht anwendbare grau', () => {
  const h = T.idsHtml(bericht());
  assert.match(h, /data-basis="1"><div class="ie-ids-kopf"><b class="ie-ids-titel">H\+K HLKS-Basis<\/b> <span class="ie-basis">IDS-Basis H\+K<\/span>/);
  assert.match(h, /data-basis="0"><div class="ie-ids-kopf"><b class="ie-ids-titel">Testregel Bauherr \(fiktiv\)<\/b>/);
  assert.match(h, /data-ergebnis="bestanden"><span class="ie-regel-name">Rohre mit Nennweite bzw\. Aussendurchmesser<\/span> <span class="ie-ergebnis ok">bestanden<\/span>/);
  assert.match(h, /ie-ergebnis leise">nicht anwendbar \(Fassung\)<\/span>.*gilt für IFC2X3, die Datei ist IFC4/);
  assert.match(h, /<details open><summary>.*Rohre mit Freigabevermerk.*nicht bestanden.*71 anwendbar, 71 nicht bestanden/);
  assert.match(h, /data-kz="KW-1\.01-SP" title="IfcPipeSegment: Eigenschaft «Pset_Bauherr»\.«Freigabe» fehlt">KW-1\.01-SP<\/button>/);
  assert.match(h, /data-kz="KW-1\.02-1"/);
  assert.match(h, /… und 69 weitere/);
  // bestandene Regeln ohne Befunde haben kein details
  assert.ok(!/Rohre mit Nennweite[^]*?<details/.test(h.split('Testregel Bauherr')[0]));
  assert.match(T.idsHtml({ ids: [] }), /Keine IDS geprüft/);
});

test('Bericht: Hinweise (eingeklappt) und Warnungen (offen)', () => {
  const h = T.hinweiseHtml(bericht());
  assert.match(h, /ie-warnungen"><h4>Warnungen \(1\)<\/h4>/);
  assert.match(h, /<details class="ie-abschnitt ie-hinweise"><summary><b>Hinweise \(1\)<\/b><\/summary>/);
  assert.equal(T.hinweiseHtml({}), '');
});

test('Kennzeichen zum Wählen: Anschluss «Element.Port» und «Dämmung Element» führen zum Element; Punkte im Kennzeichen bleiben zuerst ganz', () => {
  assert.deepEqual(T.kennzeichenKandidaten('KW-1.01-SP'), ['KW-1.01-SP', 'KW-1']);
  assert.deepEqual(T.kennzeichenKandidaten('WC-E.02.Zulauf'), ['WC-E.02.Zulauf', 'WC-E.02', 'WC-E']);
  assert.deepEqual(T.kennzeichenKandidaten('Dämmung RO-1'), ['Dämmung RO-1', 'RO-1']);
  assert.deepEqual(T.kennzeichenKandidaten('#4711'), []);
  assert.deepEqual(T.kennzeichenKandidaten(''), []);
  assert.deepEqual(T.kennzeichenKandidaten(null), []);
});

test('Zusammenfassung für die Meldung', () => {
  assert.equal(T.zusammenfassung(bericht()), 'MFH Musterweg (Beispiel, fiktiv)_IFC4_20261020-1604.ifc: 148 Elemente, Schemaprüfung fehlerfrei, IDS nicht bestanden (1 Regel nicht bestanden).');
  const b = bericht(); b.ergebnis = { schemapruefung: 'fehlerfrei', ids: 'bestanden', idsRegelnNichtBestanden: 0 }; b.anzahl.elemente = 1;
  assert.match(T.zusammenfassung(b), /: 1 Element, Schemaprüfung fehlerfrei, IDS bestanden\.$/);
});

// ------------------------------------------------------------------------------------------------ keine HTML-Einschleusung

/** Alle Werte, die der Bericht aus dem Kern übernimmt, mit Schadtext. */
function schadBericht() {
  const X = '<img src=x onerror=alert(1)>';
  const Y = '"><script>alert(2)</script>';
  const b = bericht();
  b.datei = `C:\\a\\${X}\\ausgabe\\ifc\\${Y}.ifc`;
  b.bericht = { json: `C:\\a\\${Y}.bericht.json`, md: `C:\\a\\${X}.bericht.md` };
  b.fassung = X; b.groesseBytes = 5;
  b.ergebnis = { kontrolle: X, schemapruefung: Y, ids: X, idsRegelnNichtBestanden: 1 };
  b.anzahl = { ...b.anzahl, elemente: X };
  b.jeKlasse = [{ klasse: X, entitaet: Y, typobjekt: X, predefinedType: Y, anzahl: X }];
  b.jeEntitaet = { [X]: Y };
  b.uebersprungen = [{ element: Y, klasse: X, grund: X }];
  b.uebersprungenJeGrund = { [X]: Y };
  b.schemapruefung = { ergebnis: X, werkzeug: Y, entitaetenGeprueft: X, meldungen: Y, beispiele: [X, Y] };
  b.kontrolle = { ergebnis: Y, produkte: X, ports: Y, gruppen: X, fassungGelesen: Y, einheitGelesen: X, abweichungen: [X] };
  b.ids = [{ datei: `C:\\${X}.ids`, titel: Y, basis: true, ergebnis: X, xsdMeldungen: [Y], regeln: [
    { name: X, ifcVersion: Y, ergebnis: 'nicht bestanden', anwendbar: Y, nichtBestandenElemente: X, grund: Y, befunde: [{ kennzeichen: Y, entitaet: X, gruende: [X, Y] }] },
    { name: Y, ifcVersion: X, ergebnis: X, grund: X },
  ] }];
  b.hinweise = [X]; b.warnungen = [Y];
  return b;
}

test('Keine HTML-Einschleusung: Schadtext aus dem Bericht erscheint nirgends als Tag oder Attribut', () => {
  const h = T.berichtHtml(schadBericht());
  assert.ok(!h.includes('<img'), 'img-Tag');
  assert.ok(!h.includes('<script'), 'script-Tag');
  // Ereignis-Attribute: Attributwerte vorher leeren (darin darf maskierter Text stehen)
  for (const tag of h.matchAll(/<[a-z0-9]+\s[^>]*>/g)) assert.ok(!/\son\w+=/i.test(tag[0].replace(/="[^"]*"/g, '=""')), `Ereignis-Attribut: ${tag[0].slice(0, 80)}`);
  // die einzigen Tags sind unsere
  const erlaubt = new Set(['div', 'span', 'b', 'a', 'h4', 'table', 'thead', 'tbody', 'tr', 'th', 'td', 'details', 'summary', 'ul', 'li', 'button', 'p']);
  for (const m of h.matchAll(/<\/?([a-zA-Z0-9]+)/g)) assert.ok(erlaubt.has(m[1]), `fremdes Tag <${m[1]}>`);
  // innerhalb von Attributwerten kein ungeschütztes Anführungszeichen: jede Attributzuweisung endet im selben Tag
  for (const tag of h.matchAll(/<[a-z0-9]+\s[^>]*>/g)) {
    const t = tag[0];
    assert.equal((t.match(/"/g) ?? []).length % 2, 0, `ungerade Anführungszeichen: ${t.slice(0, 120)}`);
  }
  // maskiert vorhanden
  assert.ok(h.includes('&lt;img src=x onerror=alert(1)&gt;'));
  assert.ok(h.includes('&quot;&gt;&lt;script&gt;alert(2)&lt;/script&gt;'));
});

test('Keine HTML-Einschleusung: Kennzeichen im data-kz-Attribut und im Tooltip sind maskiert', () => {
  const h = T.idsHtml(schadBericht()) + T.uebersprungenHtml(schadBericht());
  const kn = [...h.matchAll(/<button type="button" class="ie-kz" data-kz="([^"]*)"/g)];
  assert.ok(kn.length >= 2);
  for (const k of kn) assert.ok(!/[<>"]/.test(k[1]), `unmaskiert: ${k[1]}`);
  assert.equal(T.kennzeichenKnopf('A"B<C', 'x"y'), '<button type="button" class="ie-kz" data-kz="A&quot;B&lt;C" title="x&quot;y">A&quot;B&lt;C</button>');
});

test('Leere und unvollständige Berichte werfen nicht', () => {
  for (const b of [{}, { ids: null, jeKlasse: null }, { anzahl: {}, ergebnis: {} }]) assert.doesNotThrow(() => T.berichtHtml(b));
  assert.match(T.berichtHtml({}), /Keine\./);
});

// ------------------------------------------------------------------------------------------------ Verdrahtung

test('Verdrahtung: Aktion im Register Datei › Ausgabe, nur mit offenem Projekt; Dialog nur über die Kern-Tür', () => {
  const a = lies('./aktionen-ifc.js');
  assert.match(a, /id: 'datei\.ifc-export', titel: 'IFC exportieren', symbol: 'ifcExport', kuerzel: null, register: 'datei', gruppe: 'Ausgabe'/);
  assert.match(a, /aktiv: \(\) => !!zustand\.status\?\.offen/);
  assert.match(lies('./aktionen.js'), /import '\.\/aktionen-ifc\.js'/);
  assert.match(lies('./menueband.js'), /datei: \['Projekt', 'Ausgabe'\]/);
  assert.match(lies('./menueband.js'), /const REGISTER = \[\['datei', 'Datei'\], \['architektur', 'Architektur'\], \['huelle', 'Hülle'\], \['gebaeudetechnik', 'Gebäudetechnik'\], \['einfuegen', 'Einfügen'\], \['beschriften', 'Beschriften'\], \['aendern', 'Ändern'\], \['koordination', 'Koordination'\], \['ansicht', 'Ansicht'\], \['xr', 'XR'\], \['verwalten', 'Verwalten'\]\]/);
  const d = lies('./ifc-export-dialog.js');
  assert.match(d, /senden\('\/api\/ifc\/export', T\.anfrageAus\(a\)\)/);
  assert.match(d, /senden\('\/api\/ordner\/zeigen'/);
  assert.match(d, /titel: 'IFC exportieren'/);
  assert.ok(!/\$\{(?!esc\(|T\.)[^}]*\bb\./.test(d), 'Werte des Berichts gehen nur maskiert (esc) oder über T.berichtHtml in den DOM');
  assert.match(lies('../../index.html'), /css\/ifc-export\.css/);
  assert.ok(lies('../../css/ifc-export.css').includes('.ie-kz'));
});

test('Symbol «ifcExport» in beiden Sätzen: Strich und Revit-Stil (32 und 16), mit Eintrag in der Zuordnungstabelle', () => {
  assert.ok(S.SYMBOLNAMEN_STRICH.includes('ifcExport'));
  assert.notEqual(S.symbol('ifcExport'), S.symbol('gibtsnicht'));
  assert.ok(RV.hatRevitFassung('ifcExport'));
  assert.ok(RV.revitInhalt('ifcExport', false) && RV.revitInhalt('ifcExport', true));
  assert.notEqual(RV.revitInhalt('ifcExport', false), RV.revitInhalt('ifcExport', true));
  const z = RV.ZUORDNUNG.find((x) => x[0] === 'ifcExport');
  assert.deepEqual(z.slice(0, 3), ['ifcExport', 'IFC exportieren', 'IFC exportieren']);
});
